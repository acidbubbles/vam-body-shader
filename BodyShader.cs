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
    private JSONStorableStringChooser _shaderJSON;
    private bool _dirty;
    private DAZCharacter _character;
    private Dictionary<Material, Shader> _original = new Dictionary<Material, Shader>();
    private DAZHairGroup _hair;

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
            _shaderJSON = new JSONStorableStringChooser("Shader", shaders, DefaultShaderKey, "Shaders", (string val) => _dirty = true);
            _shaderJSON.storeType = JSONStorableParam.StoreType.Physical;
            RegisterStringChooser(_shaderJSON);
            var linkPopup = CreateScrollablePopup(_shaderJSON);
            linkPopup.popupPanelHeight = 1200f;
            // TODO: Find a way to see the full names when open, otherwise it's useless. Worst case, only keep the end.
            // linkPopup.labelWidth = 1200f;
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

            if (_shaderJSON.val == DefaultShaderKey)
            {
                if (_original != null)
                {
                    foreach (var x in _original)
                        x.Key.shader = x.Value;
                }
                _dirty = false;
                return;
            }

            _character = _selector.selectedCharacter;
            if (_character == null) return;
            var skin = _character.skin;
            if (skin == null) return;
            _hair = _selector.selectedHairGroup;

            var shader = Shader.Find(_shaderJSON.val);
            if (shader == null) return;

            if (_original == null)
            {
                foreach (var mat in skin.GPUmaterials)
                {
                    _original.Add(mat, mat.shader);
                }
            }

            foreach (var mat in skin.GPUmaterials)
            {
                mat.shader = shader;
            }

            var hairMaterial = _hair?.GetComponentInChildren<MeshRenderer>()?.material;
            if (hairMaterial != null)
            {
                hairMaterial.shader = shader;
            }

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

