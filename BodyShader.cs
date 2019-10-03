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

    public static readonly string[] SkinMaterials = new[]
    {
            "defaultMat",
            "Genitalia",
            "Anus",
            "Cornea",
            "Ears",
            "Eyelashes",
            "EyeReflection",
            "Face",
            "Feet",
            "Fingernails",
            "Forearms",
            "Gums",
            "Hands",
            "Head",
            "Hips",
            "InnerMouth",
            "Irises",
            "Lacrimals",
            "Legs",
            "Lips",
            "Neck",
            "Nipples",
            "Nostrils",
            "Pupils",
            "Sclera",
            "Shoulders",
            "Tear",
            "Teeth",
            "Toenails",
            "Tongue",
            "Torso"
        };

    private Atom _person;
    private DAZCharacterSelector _selector;
    private bool _dirty;
    private DAZCharacter _character;
    private Dictionary<Material, Shader> _original;
    private List<MapSettings> _map = new List<MapSettings>();
    private DAZHairGroup _hair;

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

            InitControls();

            _dirty = true;
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to init: " + e);
            DestroyImmediate(this);
        }
    }

    private void InitControls()
    {
        try
        {
            var shaders = UnityEngine.Resources
                .FindObjectsOfTypeAll(typeof(Shader))
                .Cast<Shader>()
                .Where(s => s != null)
                .Select(s => s.name)
                .Where(s => !string.IsNullOrEmpty(s) && !s.StartsWith("__"))
                .OrderBy(s => s)
                .ToList();
            foreach (var mat in SkinMaterials)
            {
                var settings = new MapSettings { MaterialName = mat };
                _map.Add(settings);
                var shaderJSON = new JSONStorableStringChooser($"Shader {settings.MaterialName}", shaders, DefaultShaderKey, $"Shaders {settings.MaterialName}", (string val) =>
                {
                    settings.ShaderName = val;
                    _dirty = true;
                });
                shaderJSON.storeType = JSONStorableParam.StoreType.Physical;
                RegisterStringChooser(shaderJSON);
                var linkPopup = CreateScrollablePopup(shaderJSON, true);
                linkPopup.popupPanelHeight = 1200f;
                // TODO: Find a way to see the full names when open, otherwise it's useless. Worst case, only keep the end.
                // linkPopup.labelWidth = 1200f;

                var alphaJSON = new JSONStorableFloat($"Alpha {settings.MaterialName}", 0f, (float val) =>
                {
                    settings.Alpha = val;
                    _dirty = true;
                }, -1f, 1f);
                RegisterFloat(alphaJSON);
                CreateSlider(alphaJSON, true);

                var renderQueue = new JSONStorableFloat($"Render Queue {settings.MaterialName}", 1999f, (float val) =>
                {
                    settings.RenderQueue = (int)Math.Round(val);
                    _dirty = true;
                }, -1f, 5000f);
                RegisterFloat(renderQueue);
                CreateSlider(renderQueue, true);
            }
        }
        catch (Exception e)
        {
            SuperController.LogError("Failed to init controls: " + e);
        }
    }

    public void Update()
    {
        try
        {
            if (!_dirty)
            {
                if (_selector.selectedCharacter != _character)
                    _dirty = true;
                else if (_selector.selectedHairGroup != _hair)
                    _dirty = true;

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

            _character = _selector.selectedCharacter;
            if (_character == null) return;
            var skin = _character.skin;
            if (skin == null) return;
            _hair = _selector.selectedHairGroup;

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
                var mat = skin.GPUmaterials.FirstOrDefault(x => x.name == setting.MaterialName);
                if (mat == null) continue;
                mat.shader = shader;
                mat.SetFloat("_AlphaAdjust", setting.Alpha);
                mat.renderQueue = setting.RenderQueue;
            }

            // var hairMaterial = _hair?.GetComponentInChildren<MeshRenderer>()?.material;
            // if (hairMaterial != null)
            // {
            //     hairMaterial.shader = shader;
            // }

            skin.BroadcastMessage("OnApplicationFocus", true);
            _dirty = false;
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

