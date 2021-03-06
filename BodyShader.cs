using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Body Shader 1.0.0, by Acid Bubbles
/// Concept developed by Skeet
/// Allows applying custom shaders to persons
/// </summary>
public class BodyShader : MVRScript
{
    private const string DefaultShaderKey = "Keep default";

    public static readonly Dictionary<string, string[]> GroupedMaterials = new Dictionary<string, string[]>
    {
        {
            "Skin",
             new[]
            {
                "defaultMat",
                "Anus",
                "Ears",
                "Face",
                "Feet",
                "Forearms",
                "Genitalia",
                "Hands",
                "Head",
                "Hips",
                "Legs",
                "Neck",
                "Nipples",
                "Nostrils",
                "Lips",
                "Shoulders",
                "Torso"
            }
        },
        {
            "Face Internals",
             new[]
            {
                "Cornea",
                "Eyelashes",
                "EyeReflection",
                "Gums",
                "InnerMouth",
                "Irises",
                "Lacrimals",
                "Pupils",
                "Sclera",
                "Tear",
                "Teeth",
                "Tongue",
            }
        },
        {
            "Nails",
             new[]
            {
                "Fingernails",
                "Toenails",
            }
        }
        };

    private Atom _person;
    private DAZCharacterSelector _selector;
    private bool _dirty;
    private DAZCharacter _character;
    private Dictionary<Material, Shader> _original;
    private readonly List<MapSettings> _map = new List<MapSettings>();
    // private DAZHairGroup[] _hair;
    private JSONStorableStringChooser _applyToJSON;
    private JSONStorableStringChooser _shaderJSON;
    private JSONStorableFloat _alphaJSON;
    private JSONStorableFloat _renderQueue;

    private class MapSettings
    {
        public string ShaderName { get; set; }
        public string MaterialName { get; set; }
        public float Alpha { get; set; }
        public int RenderQueue { get; set; }
    }

    public override void Init()
    {
        try
        {
            if (containingAtom?.type != "Person")
            {
                SuperController.LogError($"This plugin only works on Person atoms");
                DestroyImmediate(this);
                return;
            }

            _person = containingAtom;
            _selector = _person.GetComponentInChildren<DAZCharacterSelector>();

            InitSettings();
            InitControls();

            _dirty = true;
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to init: " + e);
            DestroyImmediate(this);
        }
    }

    private void InitSettings()
    {
        foreach (var mat in GroupedMaterials.Values.SelectMany(v => v).Distinct())
        {
            var settings = new MapSettings { MaterialName = mat };
            _map.Add(settings);
        }
    }

    private void InitControls()
    {
        try
        {
            var refreshShadersJSON = new JSONStorableAction("Refresh shaders", () => _applyToJSON.choices = ScanShaders());
            CreateButton("Refresh shaders", false).button.onClick.AddListener(() => _shaderJSON.choices = ScanShaders());

            var groups = GroupedMaterials.Keys.ToList();
            _applyToJSON = new JSONStorableStringChooser("Apply to...", groups, groups.FirstOrDefault(), "Apply to...", (string _) => _shaderJSON.valNoCallback = "");
            var applyToPopup = CreateScrollablePopup(_applyToJSON, false);
            applyToPopup.popupPanelHeight = 1200f;

            _shaderJSON = new JSONStorableStringChooser("Shader", ScanShaders(), DefaultShaderKey, $"Shader", (string val) => ApplyToGroup());
            var shaderPopup = CreateScrollablePopup(_shaderJSON, true);
            shaderPopup.popupPanelHeight = 1200f;
            // TODO: Find a way to see the full names when open, otherwise it's useless. Worst case, only keep the end.
            // linkPopup.labelWidth = 1200f;

            _alphaJSON = new JSONStorableFloat($"Alpha", 0f, (float val) => ApplyToGroup(), -1f, 1f);
            CreateSlider(_alphaJSON, true);

            _renderQueue = new JSONStorableFloat($"Render Queue", 1999f, (float val) => ApplyToGroup(), -1f, 5000f);
            CreateSlider(_renderQueue, true);
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to init controls: " + e);
        }
    }

    private static List<string> ScanShaders()
    {
        return UnityEngine.Resources
            .FindObjectsOfTypeAll(typeof(Shader))
            .Cast<Shader>()
            .Where(s => s != null)
            .Select(s => s.name)
            .Where(s => !string.IsNullOrEmpty(s) && !s.StartsWith("__"))
            .OrderBy(s => s)
            .ToList();
    }

    private void ApplyToGroup()
    {
        var group = _applyToJSON.val;
        string[] materialNames;
        if (!GroupedMaterials.TryGetValue(group, out materialNames))
            return;

        foreach (var materialName in materialNames)
        {
            var setting = _map.FirstOrDefault(m => m.MaterialName == materialName);
            if (setting == null) continue;
            setting.ShaderName = _shaderJSON.val;
            setting.Alpha = _alphaJSON.val;
            setting.RenderQueue = (int)Math.Round(_renderQueue.val);
        }

        _dirty = true;
    }

    public void Update()
    {
        try
        {
            if (!_dirty)
            {
                if (_selector.selectedCharacter != _character)
                    _dirty = true;
                // else if (_selector.hairItems.FirstOrDefault(h => h.active) != _hair?.FirstOrDefault())
                //     _dirty = true;

                return;
            }

            // if (shaderJSON.val == DefaultShaderKey)
            // {
            //     if (_original != null)
            //     {
            //         foreach (var x in _original)
            //             x.Key.shader = x.Value;
            //     }
            //     _dirty = false;
            //     return;
            // }

            _dirty = false;
            _character = _selector.selectedCharacter;
            if (_character == null) return;
            var skin = _character.skin;
            if (skin == null) return;
            // _hair = _selector.hairItems.Where(h => h.active).ToArray();

            // SuperController.LogMessage(string.Join(", ", skin.GPUmaterials.Select(m => m.name).OrderBy(n => n).ToArray()));
            // SuperController.LogMessage(string.Join(", ", _map.Select(m => m.Key).OrderBy(n => n).ToArray()));

            if (_original == null)
            {
                _original = new Dictionary<Material, Shader>();
                foreach (var mat in skin.GPUmaterials)
                {
                    _original.Add(mat, mat.shader);
                }
            }

            foreach (var setting in _map)
            {
                if (setting.ShaderName == DefaultShaderKey || setting.ShaderName == null) continue;
                var shader = Shader.Find(setting.ShaderName);
                if (shader == null) return;
                var mat = skin.GPUmaterials.FirstOrDefault(x => x.name.StartsWith(setting.MaterialName));
                if (mat == null) continue;
                mat.shader = shader;
                mat.SetFloat("_AlphaAdjust", setting.Alpha);
                mat.renderQueue = setting.RenderQueue;
            }

            // if (_hair != null)
            // {
            //     foreach (var hair in _hair)
            //     {
            //         var hairMaterial = hair?.GetComponentInChildren<MeshRenderer>()?.material;
            //         if (hairMaterial != null)
            //         {
            //             hairMaterial.shader = shader;
            //         }
            //     }
            // }

            skin.BroadcastMessage("OnApplicationFocus", true);
        }
        catch (Exception e)
        {
            SuperController.LogError("something failed: " + e);
        }
    }

    public void OnDisable()
    {
        try
        {
            _dirty = false;
            if (_original != null)
            {
                foreach (var x in _original)
                    x.Key.shader = x.Value;
            }
            _character?.skin?.BroadcastMessage("OnApplicationFocus", true);
        }
        catch (Exception e)
        {
            SuperController.LogError("something failed: " + e);
        }
    }

    public void OnDestroy()
    {
        OnDisable();
    }
}

