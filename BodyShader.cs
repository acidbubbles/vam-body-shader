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
    private Atom _person;
    private DAZCharacterSelector _selector;
    private JSONStorableBool _hideCharacterToggleJSON;
    private JSONStorableBool _hideCharacterJSON;

    private SkinHandler _skinHandler;
    // For change detection purposes
    private DAZCharacter _character;

    private bool _lastActive;
    // Requires re-generating all shaders and materials, either because last frame was not ready or because something changed
    private bool _dirty;
    // To avoid spamming errors when something failed
    private bool _failedOnce;
    // When waiting for a model to load, how long before we abandon
    private int _tryAgainAttempts;
    bool once;
    private JSONStorableStringChooser _shaderJSON;

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
        }
        catch (Exception e)
        {
            SuperController.LogError("something failed: " + e);
            DestroyImmediate(this);
        }
    }

    private void InitControls()
    {
        try
        {
            var shaders = GameObject.FindObjectsOfType<Shader>().Select(s => s.name).ToList();
            _shaderJSON = new JSONStorableStringChooser("Shader", shaders, "None", "Shaders", OnShaderChanged);
            _shaderJSON.storeType = JSONStorableParam.StoreType.Physical;
            RegisterStringChooser(_shaderJSON);
            var linkPopup = CreateScrollablePopup(_shaderJSON);
            linkPopup.popupPanelHeight = 600f;
            if (!string.IsNullOrEmpty(_shaderJSON.val))
                OnShaderChanged(_shaderJSON.val);
        }
        catch (Exception e)
        {
            if (_failedOnce) return;
            _failedOnce = true;
            SuperController.LogError("something failed on prerender: " + e);
        }
    }

    private void OnShaderChanged(string val)
    {
        _dirty = true;
    }

    public void OnDisable()
    {
        try
        {
            _dirty = false;
            ApplyAll(false);
            _lastActive = false;
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

    public void Update()
    {
        try
        {
            if (!_lastActive)
            {
                ApplyAll(true);
                _lastActive = true;
            }
            else if (_dirty)
            {
                _dirty = false;
                ApplyAll(_lastActive);
            }
            else if (_lastActive && _selector.selectedCharacter != _character)
            {
                _skinHandler?.Restore();
                _skinHandler = null;
                ApplyAll(true);
            }
        }
        catch (Exception e)
        {
            if (_failedOnce) return;
            _failedOnce = true;
            SuperController.LogError("something failed: " + e);
        }
    }

    private void ApplyAll(bool active)
    {
        // Try again next frame
        if (_selector.selectedCharacter?.skin == null)
        {
            MakeDirty("Skin not yet loaded.");
            return;
        }

        _character = _selector.selectedCharacter;

        if (UpdateHandler(ref _skinHandler, active && _hideCharacterJSON.val))
            ConfigureHandler("Skin", ref _skinHandler, _skinHandler.Configure(_character.skin));
        if (!_dirty) _tryAgainAttempts = 0;
    }

    private void MakeDirty(string reason)
    {
        _dirty = true;
        _tryAgainAttempts++;
        if (_tryAgainAttempts > 90 * 20) // Approximately 20 to 40 seconds
        {
            SuperController.LogError("something failed. Reason: " + reason + ".");
            enabled = false;
        }
    }

    private void ConfigureHandler<T>(string what, ref T handler, int result)
     where T : IHandler, new()
    {
        switch (result)
        {
            case HandlerConfigurationResult.Success:
                break;
            case HandlerConfigurationResult.CannotApply:
                handler = default(T);
                break;
            case HandlerConfigurationResult.TryAgainLater:
                handler = default(T);
                MakeDirty(what + " is still waiting for assets to be ready.");
                break;
        }
    }

    private bool UpdateHandler<T>(ref T handler, bool active)
     where T : IHandler, new()
    {
        if (handler == null && active)
        {
            handler = new T();
            return true;
        }

        if (handler != null && active)
        {
            handler.Restore();
            handler = new T();
            return true;
        }

        if (handler != null && !active)
        {
            handler.Restore();
            handler = default(T);
        }

        return false;
    }

    public static class HandlerConfigurationResult
    {
        public const int Success = 0;
        public const int CannotApply = 1;
        public const int TryAgainLater = 2;
    }

    public interface IHandler
    {
        void Restore();
    }

    public class SkinHandler : IHandler
    {
        public class SkinShaderMaterialReference
        {
            public Material material;
            public Shader originalShader;
            public float originalAlphaAdjust;
            public float originalColorAlpha;
            public Color originalSpecColor;

            public static SkinShaderMaterialReference FromMaterial(Material material)
            {
                var materialRef = new SkinShaderMaterialReference();
                materialRef.material = material;
                materialRef.originalShader = material.shader;
                materialRef.originalAlphaAdjust = material.GetFloat("_AlphaAdjust");
                materialRef.originalColorAlpha = material.GetColor("_Color").a;
                materialRef.originalSpecColor = material.GetColor("_SpecColor");
                return materialRef;
            }
        }

        public static readonly string[] MaterialsToHide = new[]
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

        public static IList<Material> GetMaterialsToHide(DAZSkinV2 skin)
        {

            var materials = new List<Material>(MaterialsToHide.Length);


            foreach (var material in skin.GPUmaterials)
            {
                if (!MaterialsToHide.Any(materialToHide => material.name.StartsWith(materialToHide)))
                    continue;

                materials.Add(material);
            }

            return materials;
        }

        private static Dictionary<string, Shader> ReplacementShaders = new Dictionary<string, Shader>
            {
                // Opaque materials
                { "Custom/Subsurface/GlossCullComputeBuff", Shader.Find("Custom/Subsurface/TransparentGlossSeparateAlphaComputeBuff") },
                { "Custom/Subsurface/GlossNMCullComputeBuff", Shader.Find("Custom/Subsurface/TransparentGlossNMSeparateAlphaComputeBuff") },
                { "Custom/Subsurface/GlossNMDetailCullComputeBuff", Shader.Find("Custom/Subsurface/TransparentGlossNMDetailNoCullSeparateAlphaComputeBuff") },
                { "Custom/Subsurface/CullComputeBuff", Shader.Find("Custom/Subsurface/TransparentSeparateAlphaComputeBuff") },
                { "Custom/Subsurface/GlossNMTessMappedFixedComputeBuff", Shader.Find("Custom/Subsurface/TransparentGlossNMSeparateAlphaComputeBuff") },
                // Transparent materials
                { "Custom/Subsurface/TransparentGlossNoCullSeparateAlphaComputeBuff", null },
                { "Custom/Subsurface/TransparentGlossComputeBuff", null },
                { "Custom/Subsurface/TransparentComputeBuff", null },
                { "Custom/Subsurface/AlphaMaskComputeBuff", null },
				//{ "Custom/Subsurface/GlossNMTessMappedFixedComputeBuff", null },
                { "Marmoset/Transparent/Simple Glass/Specular IBLComputeBuff", null },
            };

        private DAZSkinV2 _skin;
        private List<SkinShaderMaterialReference> _materialRefs;

        public int Configure(DAZSkinV2 skin)
        {
            _skin = skin;
            _materialRefs = new List<SkinShaderMaterialReference>();

            foreach (var material in GetMaterialsToHide(skin))
            {
#if (IMPROVED_POV)
                if(material == null)
                    throw new InvalidOperationException("Attempts to apply the shader strategy on a destroyed material.");

                if (material.GetInt(SkinShaderMaterialReference.ImprovedPovEnabledShaderKey) == 1)
                    throw new InvalidOperationException("Attempts to apply the shader strategy on a skin that already has the plugin enabled (shader key).");
#endif

                var materialInfo = SkinShaderMaterialReference.FromMaterial(material);

                Shader shader;
                if (!ReplacementShaders.TryGetValue(material.shader.name, out shader))
                    SuperController.LogError("Missing replacement shader: '" + material.shader.name + "'");

                if (shader != null) material.shader = shader;

                _materialRefs.Add(materialInfo);
            }

            // This is a hack to force a refresh of the shaders cache
            skin.BroadcastMessage("OnApplicationFocus", true);
            return HandlerConfigurationResult.Success;
        }

        public void Restore()
        {
            foreach (var material in _materialRefs)
                material.material.shader = material.originalShader;

            _materialRefs = null;

            // This is a hack to force a refresh of the shaders cache
            _skin.BroadcastMessage("OnApplicationFocus", true);
        }
    }
}

