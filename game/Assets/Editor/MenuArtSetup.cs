using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Menu-1 (docs/26 §3.5): import settings and 9-slice borders for the magic-library menu parts.
    // tools/artwork/package_menu_ui.py copies the PNGs into Folder; this only configures their importers.
    public static class MenuArtSetup
    {
        public const string Folder = "Assets/Art/UI/Menu";

        public const string Background = "UI_Menu_Background_Library";
        public const string PanelMain = "UI_Menu_Panel_Main_9s";
        public const string PanelRoster = "UI_Menu_Panel_Roster_9s";
        public const string PanelDetails = "UI_Menu_Panel_Details_9s";
        public const string TitlePlaque = "UI_Menu_TitlePlaque";
        public const string ButtonPrimary = "UI_Menu_Button_Primary_9s";
        public const string ButtonSecondary = "UI_Menu_Button_Secondary_9s";
        public const string SectionRibbon = "UI_Menu_SectionRibbon_9s";
        public const string SliderTrack = "UI_Menu_Slider_Track_9s";
        public const string SliderFill = "UI_Menu_Slider_Fill_9s";
        public const string RingPill = "UI_Menu_Ring_Pill_9s";
        public const string RingRounded = "UI_Menu_Ring_Rounded_9s";
        public const string Clover = "UI_Menu_Clover";
        public const string Checkbox = "UI_Menu_Checkbox";
        public const string Checkmark = "UI_Menu_Checkmark";
        public const string ArrowLeft = "UI_Menu_ArrowLeft";
        public const string PortraitSlot = "UI_Menu_PortraitSlot";
        public const string Pedestal = "UI_Menu_Pedestal";
        public const string SilhouetteComingSoon = "UI_Menu_SilhouetteComingSoon";
        public const string IconMusic = "UI_Menu_Icon_Music";
        public const string IconStarburst = "UI_Menu_Icon_Starburst";
        public const string IconHeart = "UI_Menu_Icon_Heart";
        public const string IconWand = "UI_Menu_Icon_Wand";
        public const string IconSword = "UI_Menu_Icon_Sword";
        public const string IconSkillJunior = "UI_Menu_Icon_SkillJunior";
        public const string IconSkillSenior = "UI_Menu_Icon_SkillSenior";
        public const string IconSpeaker = "UI_Menu_Icon_Speaker";
        public const string IconSparkles = "UI_Menu_Icon_Sparkles";
        public const string IconMonitor = "UI_Menu_Icon_Monitor";
        public const string IconFullscreen = "UI_Menu_Icon_Fullscreen";
        public const string IconList = "UI_Menu_Icon_List";
        public const string IconCrown = "UI_Menu_Icon_Crown";
        public const string IconLock = "UI_Menu_Icon_Lock";
        public const string SkillLowGrade = "UI_Menu_Skill_LowGrade";
        public const string SkillBasic = "UI_Menu_Skill_Basic";
        public const string Glow = "UI_Menu_Glow_9s";
        public const string RoundedFill = "UI_Menu_RoundedFill_9s";
        public const string Slash = "UI_Menu_Slash";

        // Unity spriteBorder order: left, bottom, right, top. Corners, button caps and the ribbon notch stay unstretched.
        public static readonly IReadOnlyDictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            [PanelMain] = new(72, 72, 72, 72),
            [PanelRoster] = new(64, 64, 64, 64),
            [PanelDetails] = new(80, 72, 64, 96),
            [ButtonPrimary] = new(76, 0, 76, 0),
            [ButtonSecondary] = new(80, 0, 80, 0),
            [SectionRibbon] = new(56, 0, 64, 0),
            [SliderTrack] = new(26, 0, 26, 0),
            [SliderFill] = new(30, 0, 30, 0),
            [RingPill] = new(32, 31, 32, 31),
            [RingRounded] = new(20, 20, 20, 20),
            [Glow] = new(48, 48, 48, 48),
            [RoundedFill] = new(20, 20, 20, 20),
        };

        public static readonly string[] All =
        {
            Background, PanelMain, PanelRoster, PanelDetails, TitlePlaque, ButtonPrimary, ButtonSecondary,
            SectionRibbon, SliderTrack, SliderFill, RingPill, RingRounded, Clover, Checkbox, Checkmark, ArrowLeft,
            PortraitSlot, Pedestal, SilhouetteComingSoon, IconMusic, IconStarburst, IconHeart, IconWand, IconSword,
            IconSkillJunior, IconSkillSenior, IconSpeaker, IconSparkles, IconMonitor, IconFullscreen, IconList,
            IconCrown, IconLock, SkillLowGrade, SkillBasic, Glow, RoundedFill, Slash
        };

        public static string PathOf(string name) => $"{Folder}/{name}.png";

        [MenuItem("Trickal Fan Game/Menu/Setup Menu-1 Art Import")]
        public static void Setup()
        {
            int changed = Apply();
            Debug.Log($"Menu-1 art import ready. {All.Length} sprites, {changed} importer(s) changed.");
        }

        // Returns how many importers changed, so reruns on a configured folder report 0 and reimport nothing.
        public static int Apply()
        {
            int changed = 0;
            foreach (string name in All)
            {
                string path = PathOf(name);
                if (!File.Exists(path))
                    throw new InvalidOperationException(
                        $"Missing menu sprite {path}. Run `python tools/artwork/package_menu_ui.py` first.");
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(path) as TextureImporter;
                }
                if (importer == null) throw new InvalidOperationException("Not a texture: " + path);
                if (!Configure(importer, name)) continue;
                importer.SaveAndReimport();
                changed++;
            }
            return changed;
        }

        private static bool Configure(TextureImporter importer, string name)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            string before = EditorJsonUtility.ToJson(settings) + importer.textureCompression + importer.maxTextureSize +
                            importer.spriteBorder;

            bool background = name == Background;
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = 100;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteBorder = Borders.TryGetValue(name, out Vector4 border) ? border : Vector4.zero;
            settings.alphaSource = background ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            settings.alphaIsTransparency = !background;
            settings.sRGBTexture = true;
            settings.mipmapEnabled = false;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.filterMode = FilterMode.Bilinear;
            settings.readable = false;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = settings.spriteBorder;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            string after = EditorJsonUtility.ToJson(settings) + importer.textureCompression + importer.maxTextureSize +
                           importer.spriteBorder;
            return before != after;
        }

        public static Sprite Load(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PathOf(name));
            if (sprite == null) throw new InvalidOperationException("Menu sprite is not imported: " + PathOf(name));
            return sprite;
        }

        public static void BatchSetup()
        {
            Setup();
            AssetDatabase.SaveAssets();
        }
    }
}
