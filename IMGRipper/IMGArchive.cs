﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IMGRipper
{
    public static class CustomHasher
    {
        public static uint GetFilenameCRC(string filename)
        {
            var hash = 0u;

            for (int i = 0; i < filename.Length; i++)
            {
                var c = filename[i];

                if (c == 0)
                    break;
                
                hash = (hash + c) * 1025;
                hash = hash ^ (hash >> 6);
            }

            hash += (hash * 8);

            return ((hash >> 11)  ^ hash) * 32769;
        }
    }

    public class IMGArchive
    {
        public class Entry
        {
            // was FileName set directly?
            private bool _hasFilename;

            private string _fileName;
            private uint _fileNameHash;

            protected virtual uint CalculateHash(string fileName)
            {
                return CustomHasher.GetFilenameCRC(fileName);
            }
            
            public string FileName
            {
                get { return _fileName; }
                set
                {
                    _hasFilename = true;

                    _fileName = value;
                    _fileNameHash = CalculateHash(value);
                }
            }

            public uint FileNameHash
            {
                get { return _fileNameHash; }
                set
                {
                    _hasFilename = false;

                    _fileNameHash = value;
                    _fileName = $"{_fileNameHash}";
                }
            }

            public virtual long FileOffset
            {
                get { return (Offset * 2048L); }
            }

            public bool HasFileName
            {
                get { return _hasFilename; }
            }
            
            public int Offset { get; set; }
            public int Length { get; set; }

            public override sealed int GetHashCode()
            {
                // hopefully this will prevent any unwanted behavior
                if (_fileNameHash == 0)
                    return base.GetHashCode();

                return (int)_fileNameHash;
            }
        }

        public class XBoxEntry : Entry
        {
            public int LumpIndex { get; set; }
        }

        public class PSPEntry : Entry
        {
            protected override uint CalculateHash(string fileName)
            {
                throw new InvalidOperationException("Cannot calculate the hash for a PSPEntry (hashing method unknown)");
            }

            public override long FileOffset
            {
                // no calculation needed
                get { return Offset; }
            }

            // ???
            public int UncompressedLength { get; set; }

            public bool IsCompressed
            {
                get { return (Length != UncompressedLength); }
            }
        }

        public class FileDescriptor
        {
            public string FileName { get; set; }
            
            public int Index { get; set; }
            public int DataIndex { get; set; }

            public Entry EntryData { get; set; }
        }
        
        public int Reserved { get; set; }
        public List<Entry> Entries { get; set; }

        public bool IsLoaded { get; protected set; }

        public IMGVersion Version { get; set; }
        
        private static readonly Dictionary<uint, string> LookupTable =
            new Dictionary<uint, string>();

        private static readonly Dictionary<uint, string> HashLookup =
            new Dictionary<uint, string>();

        private List<String> HACK_CompileDriv3rFiles()
        {
            var knownFiles = new List<String>() {
                "US_GAMECONFIG.TXT",
                "EU_GAMECONFIG.TXT",
                "AS_GAMECONFIG.TXT",

                "ANIMS\\ANIMATION_MIAMI.AB3",
                "ANIMS\\ANIMATION_NICE.AB3",
                "ANIMS\\ANIMATION_ISTANBUL.AB3",

                "ANIMS\\ANIMATIONLOOKUP.TXT",
                "ANIMS\\MACROLIST.TXT",

                "ANIMS\\MACROS\\FEMALE_DIE_BACKWARDS.ANM",
                "ANIMS\\MACROS\\FEMALE_DIE_FORWARDS.ANM",
                "ANIMS\\MACROS\\FEMALE_DIE_LEFT.ANM",
                "ANIMS\\MACROS\\FEMALE_IDLE_LOOK_WATCH.ANM",
                "ANIMS\\MACROS\\FEMALE_IDLE_SCRATCH.ANM",
                "ANIMS\\MACROS\\FEMALE_IDLE_STANDING.ANM",
                "ANIMS\\MACROS\\FEMALE_IDLE_STAND_SIT_C.ANM",
                "ANIMS\\MACROS\\FEMALE_IDLE_TAP_FOOT.ANM",
                "ANIMS\\MACROS\\FEMALE_IDLE_THINK.ANM",
                "ANIMS\\MACROS\\FEMALE_IDLE_WEIGHT_SHIFT.ANM",
                "ANIMS\\MACROS\\FEMALE_IGCS_M08B_MID_CAL_01.ANM",
                "ANIMS\\MACROS\\FEMALE_RUN.ANM",
                "ANIMS\\MACROS\\FEMALE_WALK.ANM",
                "ANIMS\\MACROS\\MALE_2HANDED_SHOOT_CROUCHING_AHEAD.ANM",
                "ANIMS\\MACROS\\MALE_2HANDED_SHOOT_CROUCHING_AHEAD_V2.ANM",
                "ANIMS\\MACROS\\MALE_2HANDED_SHOOT_CROUCHING_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_2HANDED_SHOOT_CROUCHING_UP.ANM",
                "ANIMS\\MACROS\\MALE_2HANDED_SHOOT_CROUCHING_UP_A.ANM",
                "ANIMS\\MACROS\\MALE_ARRESTED.ANM",
                "ANIMS\\MACROS\\MALE_BIKE_BALE.ANM",
                "ANIMS\\MACROS\\MALE_CRAWL.ANM",
                "ANIMS\\MACROS\\MALE_CROUCH_TO_STAND.ANM",
                "ANIMS\\MACROS\\MALE_CROUCH_TURN.ANM",
                "ANIMS\\MACROS\\MALE_DIE_BACKWARDS.ANM",
                "ANIMS\\MACROS\\MALE_DIE_FALL.ANM",
                "ANIMS\\MACROS\\MALE_DIE_FALL_GET_UP.ANM",
                "ANIMS\\MACROS\\MALE_DIE_FLAIL.ANM",
                "ANIMS\\MACROS\\MALE_DIE_FORWARDS.ANM",
                "ANIMS\\MACROS\\MALE_DIE_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_DIE_RIGHT.ANM",
                "ANIMS\\MACROS\\MALE_DODGE_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_DODGE_LEFT_LEAP.ANM",
                "ANIMS\\MACROS\\MALE_DODGE_LEFT_ROLL.ANM",
                "ANIMS\\MACROS\\MALE_DODGE_RIGHT.ANM",
                "ANIMS\\MACROS\\MALE_DRAW_GUN_BADDIES.ANM",
                "ANIMS\\MACROS\\MALE_GET_UP_BACK.ANM",
                "ANIMS\\MACROS\\MALE_HIT_BY_CAR.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_BREATHING_TANNER.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_BREATHING_WIDER_STANCE.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_LOOK_WATCH.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_PRESS_BUTTON.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_SCRATCH.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_STANDING.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_STAND_SIT_C.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_TAP_FOOT.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_THINK.ANM",
                "ANIMS\\MACROS\\MALE_IDLE_WEIGHT_SHIFT.ANM",
                "ANIMS\\MACROS\\MALE_IGCS_MOBILE_ANSWER.ANM",
                "ANIMS\\MACROS\\MALE_IGCS_WALK_STOP.ANM",
                "ANIMS\\MACROS\\MALE_JOG.ANM",
                "ANIMS\\MACROS\\MALE_JOG_BACKWARDS.ANM",
                "ANIMS\\MACROS\\MALE_JOG_BACKWARDS_STRAFE_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_JOG_BACKWARDS_STRAFE_RIGHT_45.ANM",
                "ANIMS\\MACROS\\MALE_JOG_STRAFE_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_JOG_STRAFE_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_JOG_STRAFE_RIGHT.ANM",
                "ANIMS\\MACROS\\MALE_JOG_STRAFE_RIGHT_45.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_AIR.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_AIR_THREE.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_AIR_TWO.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_FORWARD_END.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_FORWARD_START.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_ON_SPOT_START.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_RUN_END.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_RUN_LEFT_45_END.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_RUN_LEFT_45_START.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_RUN_RIGHT_45_END.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_RUN_RIGHT_45_START.ANM",
                "ANIMS\\MACROS\\MALE_JUMP_RUN_START.ANM",
                "ANIMS\\MACROS\\MALE_LOOK_DOWN.ANM",
                "ANIMS\\MACROS\\MALE_LOOK_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_LOOK_RIGHT.ANM",
                "ANIMS\\MACROS\\MALE_LOOK_UP.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_CROUCHING_AHEAD.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_CROUCHING_AHEAD_V2.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_CROUCHING_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_CROUCHING_UP.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_CROUCHING_UP_A.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_FORWARDS_AHEAD.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_FORWARDS_AHEAD_V2.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_FORWARDS_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_FORWARDS_RIGHT.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_FORWARDS_UP.ANM",
                "ANIMS\\MACROS\\MALE_PISTOL_SHOOT_FORWARDS_UP_A.ANM",
                "ANIMS\\MACROS\\MALE_RELOAD_2HANDED.ANM",
                "ANIMS\\MACROS\\MALE_RELOAD_2HANDED_RIFLE.ANM",
                "ANIMS\\MACROS\\MALE_RELOAD_2HANDED_RIFLE_CROUCHING.ANM",
                "ANIMS\\MACROS\\MALE_RELOAD_LAUNCHER.ANM",
                "ANIMS\\MACROS\\MALE_RELOAD_SINGLE.ANM",
                "ANIMS\\MACROS\\MALE_RELOAD_SINGLE_CROUCH.ANM",
                "ANIMS\\MACROS\\MALE_ROLL_RUN.ANM",
                "ANIMS\\MACROS\\MALE_ROLL_RUN_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_ROLL_RUN_RIGHT_45.ANM",
                "ANIMS\\MACROS\\MALE_RUN.ANM",
                "ANIMS\\MACROS\\MALE_RUN_BACKWARDS.ANM",
                "ANIMS\\MACROS\\MALE_RUN_BACKWARDS_STRAFE_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_RUN_BACKWARDS_STRAFE_RIGHT_45.ANM",
                "ANIMS\\MACROS\\MALE_RUN_ROLL_END.ANM",
                "ANIMS\\MACROS\\MALE_RUN_ROLL_END_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_RUN_ROLL_END_RIGHT_45.ANM",
                "ANIMS\\MACROS\\MALE_RUN_ROLL_START.ANM",
                "ANIMS\\MACROS\\MALE_RUN_ROLL_START_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_RUN_ROLL_START_RIGHT_45.ANM",
                "ANIMS\\MACROS\\MALE_RUN_STRAFE_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_RUN_STRAFE_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_RUN_STRAFE_RIGHT.ANM",
                "ANIMS\\MACROS\\MALE_RUN_STRAFE_RIGHT_45.ANM",
                "ANIMS\\MACROS\\MALE_SEDAN_FWD_BALE.ANM",
                "ANIMS\\MACROS\\MALE_SMASH_OPEN_DOOR.ANM",
                "ANIMS\\MACROS\\MALE_SPORTS_BALE.ANM",
                "ANIMS\\MACROS\\MALE_SWIM.ANM",
                "ANIMS\\MACROS\\MALE_TAKE_HIT_BACKWARDS.ANM",
                "ANIMS\\MACROS\\MALE_TREAD_WATER.ANM",
                "ANIMS\\MACROS\\MALE_TRUCK_BALE.ANM",
                "ANIMS\\MACROS\\MALE_TURN.ANM",
                "ANIMS\\MACROS\\MALE_TURN_WIDER_STANCE.ANM",
                "ANIMS\\MACROS\\MALE_TWO_HANDED_SHOOT_FORWARDS_AHEAD.ANM",
                "ANIMS\\MACROS\\MALE_TWO_HANDED_SHOOT_FORWARDS_AHEAD_2.ANM",
                "ANIMS\\MACROS\\MALE_TWO_HANDED_SHOOT_FORWARDS_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_TWO_HANDED_SHOOT_FORWARDS_RIGHT.ANM",
                "ANIMS\\MACROS\\MALE_TWO_HANDED_SHOOT_FORWARDS_UP.ANM",
                "ANIMS\\MACROS\\MALE_TWO_HANDED_SHOOT_FORWARDS_UP_A.ANM",
                "ANIMS\\MACROS\\MALE_WALK.ANM",
                "ANIMS\\MACROS\\MALE_WALK_BACKWARDS.ANM",
                "ANIMS\\MACROS\\MALE_WALK_BACKWARDS_STRAFE_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_WALK_BACKWARDS_STRAFE_RIGHT_45.ANM",
                "ANIMS\\MACROS\\MALE_WALK_STRAFE_LEFT.ANM",
                "ANIMS\\MACROS\\MALE_WALK_STRAFE_LEFT_45.ANM",
                "ANIMS\\MACROS\\MALE_WALK_STRAFE_RIGHT.ANM",
                "ANIMS\\MACROS\\MALE_WALK_STRAFE_RIGHT_45.ANM",

                "ANIMS\\SKELETON_MACROS\\FEMALE_SKELETON.SKM",
                "ANIMS\\SKELETON_MACROS\\MALE_SKELETON.SKM",

                "FMV\\CONTROLSCREENBG.XMV",
                "FMV\\FRONTEND.XMV",
                "FMV\\MAINMENU.XMV",
                "FMV\\PLACEHOLDER.XMV",
                "FMV\\UNDERCOVER.XMV",
                "FMV\\VIEWCUTSCENE.XMV",
                
                "GUNS\\GUNS.CPR",

                "MISSIONS\\PERSONALITIES\\DEFAULT.ACP",

                "OVERLAYS\\CHASE_TRAIN.BIN",
                "OVERLAYS\\DG_GENERAL.BIN",
                "OVERLAYS\\DG_GETAWAY.BIN",
                "OVERLAYS\\DG_QUICKCHASE.BIN",
                "OVERLAYS\\TAKE_A_RIDE.BIN",

                "OVERLAYS\\OVERLAYS.GFX",

                "OVERLAYS\\MIAMI.MAP",
                "OVERLAYS\\NICE.MAP",
                "OVERLAYS\\ISTANBUL.MAP",
                "OVERLAYS\\TRAIN_MI.MAP",

                "SKIES\\SKY_ABDUCTION.D3S",
                "SKIES\\SKY_ALLEYWAY.D3S",
                "SKIES\\SKY_ANOTHERLEAD.D3S",
                "SKIES\\SKY_ARMSDEAL.D3S",
                "SKIES\\SKY_ARTICWAGON.D3S",
                "SKIES\\SKY_BOMBTRUCK.D3S",
                "SKIES\\SKY_CALITAINTROUBLE.D3S",
                "SKIES\\SKY_CALITASTAIL.D3S",
                "SKIES\\SKY_CHASETHETRAIN.D3S",
                "SKIES\\SKY_COUNTDOWN.D3S",
                "SKIES\\SKY_DODGEISLAND.D3S",
                "SKIES\\SKY_HUNTED.D3S",
                "SKIES\\SKY_IMPRESSLOMAZ.D3S",
                "SKIES\\SKY_IMPRESSLOMAZSUNSET.D3S",
                "SKIES\\SKY_ITAKERIDEDUSKR.D3S",
                "SKIES\\SKY_ITAKRIDEDAWNR.D3S",
                "SKIES\\SKY_LITTLEHAVANA.D3S",
                "SKIES\\SKY_MOUNTAINCHASE.D3S",
                "SKIES\\SKY_MTAKERIDEDAY.D3S",
                "SKIES\\SKY_MTAKERIDENIGHTR.D3S",
                "SKIES\\SKY_NTAKERIDEDAWNR.D3S",
                "SKIES\\SKY_NTAKERIDEDAYR.D3S",
                "SKIES\\SKY_POLICEHQ.D3S",
                "SKIES\\SKY_RESCUEDUBOIS.D3S",
                "SKIES\\SKY_RETRIBUTION.D3S",
                "SKIES\\SKY_ROOFTOPS.D3S",
                "SKIES\\SKY_SMASHANDRUN.D3S",
                "SKIES\\SKY_SMUGGLERS.D3S",
                "SKIES\\SKY_SPEED.D3S",
                "SKIES\\SKY_SUNRISETAKERIDE.D3S",
                "SKIES\\SKY_SURVEILLANCE.D3S",
                "SKIES\\SKY_TANNERESCAPES.D3S",
                "SKIES\\SKY_THECHASE.D3S",
                "SKIES\\SKY_THESIEGE.D3S",
                "SKIES\\SKY_TRAPPED.D3S",

                "SOUNDS\\FEMUSIC.DAT",
            };

            // Cities
            var d3cTypes = new[] {
                "PS2",
                "XBOX",
            };

            var cities = new[] {
                "MIAMI",
                "NICE",
                "ISTANBUL",
            };

            foreach (var type in d3cTypes)
            {
                foreach (var city in cities)
                {
                    knownFiles.Add(String.Format("CITIES\\{0}_DAY_{1}.D3C", city, type));
                    knownFiles.Add(String.Format("CITIES\\{0}_NIGHT_{1}.D3C", city, type));
                }

                knownFiles.Add(String.Format("CITIES\\TRAIN_MI_DAY_{0}.D3C", type));
            }

            // vehicle stuff
            foreach (var city in cities)
            {
                knownFiles.Add(String.Format("CONFIGS\\VEHICLES\\BIGVO3\\{0}.BO3", city));
                knownFiles.Add(String.Format("SOUNDS\\{0}.VSB", city));

                knownFiles.Add(String.Format("VEHICLES\\{0}\\CARGLOBALS{0}.VGT", city));

                knownFiles.Add(String.Format("VEHICLES\\{0}.VVS", city));
                knownFiles.Add(String.Format("VEHICLES\\{0}.VVV", city));
                knownFiles.Add(String.Format("VEHICLES\\{0}_ARTIC_TRUCK.VVV", city));
                knownFiles.Add(String.Format("VEHICLES\\{0}_BIKE.VVV", city));
                knownFiles.Add(String.Format("VEHICLES\\{0}_BOAT.VVV", city));
            }

            // FMVs
            var fmvFiles = new[] {
                "FMV\\ATTRACT",

                "FMV\\LEGAL_AS",
                "FMV\\LEGAL_EU",
                "FMV\\LEGAL_US",

                "FMV\\RECAP",
                "FMV\\REFLECT",

                "FMV\\T3RED_EU",
                "FMV\\T3RED_US",

                "FMV\\THEMAKINGOF",

                "FMV\\SHADOWOPS_EU",
                "FMV\\SHADOWOPS_US",
            };

            foreach (var fmv in fmvFiles)
            {
                knownFiles.Add(String.Format("{0}.SBN", fmv));
                knownFiles.Add(String.Format("{0}.XMV", fmv));
            }

            var dgCities = new[] {
                "MIAMI",
                "NICE",
                "BUL",
            };

            foreach (var dgCity in dgCities)
            {
                knownFiles.Add(String.Format("FMV\\{0}CHASE.XMV", dgCity));
                knownFiles.Add(String.Format("FMV\\{0}CHECKPNT.XMV", dgCity));
                knownFiles.Add(String.Format("FMV\\{0}GATERACE.XMV", dgCity));
                knownFiles.Add(String.Format("FMV\\{0}GETAWAY.XMV", dgCity));
                knownFiles.Add(String.Format("FMV\\{0}SURVIVAL.XMV", dgCity));
                knownFiles.Add(String.Format("FMV\\{0}TRAILBLZ.XMV", dgCity));
            }

            var selCities = new[] {
                "MIAMI",
                "NICE",
                "ISTAN",
            };

            var selTimes = new[] {
                "DAWN",
                "DAY",
                "DUSK",
                "NIGHT",
            };

            foreach (var city in selCities)
            {
                for (int i = 1; i <= 30; i++)
                    knownFiles.Add(String.Format("FMV\\{0}_CAR{1:D2}.XMV", city, i));

                foreach (var time in selTimes)
                {
                    knownFiles.Add(String.Format("FMV\\{0}_{1}_DRY.XMV", city, time));
                    knownFiles.Add(String.Format("FMV\\{0}_{1}_OVR.XMV", city, time));
                    knownFiles.Add(String.Format("FMV\\{0}_{1}_WET.XMV", city, time));
                }
            }

            for (int i = 1; i <= 200; i++)
            {
                knownFiles.Add(String.Format("FMV\\IGCS{0:D2}.XMV", i));
                knownFiles.Add(String.Format("FMV\\IGCS{0:D2}.SBN", i));
            }

            var scFiles = new[] {
                "01",
                "02",
                "03",
                "04",
                "05a", "05b",
                "06",
                "07",
                "08",
                "09",
                "10", "10b",
                "11",
                "12",
                "13a", "13b",
                "14",
                "15",
                "16",
                "17a", "17b",
                "18a", "18b",
                "19",
                "20",
                "21",
                "22",
                "23",
                "24",
                "25a", "25b",
                "26",
                "27",
                "28",
                "29",
                "30",
                "31",
            };

            foreach (var sc in scFiles)
            {
                knownFiles.Add(String.Format("FMV\\SC{0}.XMV", sc));
                knownFiles.Add(String.Format("FMV\\SC{0}.SBN", sc));
            }

            // Missions
            var missionExts = new[] {
                "MPC", // PC (just in case)
                "MPS", // PS2
                "MXB", // XBox
            };

            // mission scripts
            foreach (var ext in missionExts)
            {
                for (int i = 1; i <= 200; i++)
                    knownFiles.Add(String.Format("MISSIONS\\MISSION{0:D2}.{1}", i, ext));

                // leftover scripts that may still exist
                foreach (var city in cities)
                {
                    knownFiles.Add(String.Format("MISSIONS\\TAKEARIDE{0}.{1}", city, ext));
                    knownFiles.Add(String.Format("MISSIONS\\TEMPLATE{0}.{1}", city, ext));
                }
            }

            // recordings
            var padFiles = new[] {
                "M02_COP",
            };

            foreach (var padFile in padFiles)
                knownFiles.Add(String.Format("MISSIONS\\RECORDINGS\\{0}", padFile));

            for (int i = 1; i <= 100; i++)
            {
                var misName = String.Format("MISSION{0:D2}", i);

                knownFiles.Add(String.Format("MISSIONS\\PERSONALITIES\\{0}.ACP", misName));
                knownFiles.Add(String.Format("MISSIONS\\{0}.DAM", misName));

                knownFiles.Add(String.Format("VEHICLES\\{0}.VVV", misName));
            }

            for (int i = 1; i <= 80; i++)
                knownFiles.Add(String.Format("MOODS\\MOOD{0:D2}.TXT", i));

            // GUI
            var guiFiles = new[] {
                "GUI\\BOOTUP\\BOOTUP",
                "GUI\\FD\\FILMDIRECTOR",
                "GUI\\MAIN\\FRONT",
                "GUI\\MUGAME\\MUGAME",
                "GUI\\MUMAIN\\MUMAIN",
                "GUI\\NAME\\NAME",
                "GUI\\NOCONT\\NOCONT",

                "GUI\\PAUSE\\D3PAUSE",
                "GUI\\PAUSE\\DGPAUSE",
                "GUI\\PAUSE\\LMPAUSE",
                "GUI\\PAUSE\\RPLYPAUSE",
                "GUI\\PAUSE\\TARPAUSE",

                // PS2 only
                "GUI\\BOOTUP\\BOOTUP_US",
                "GUI\\FD\\FILMDIRECTOR_US",
                "GUI\\MAIN\\FRONT_US",
                "GUI\\MUGAME\\MUGAME_US",
                "GUI\\MUMAIN\\MUMAIN_US",
                "GUI\\NAME\\NAME_US",
                "GUI\\NOCONT\\NOCONT_US",

                "GUI\\PAUSE\\D3PAUSE_US",
                "GUI\\PAUSE\\DGPAUSE_US",
                "GUI\\PAUSE\\LMPAUSE_US",
                "GUI\\PAUSE\\RPLYPAUSE_US",
                "GUI\\PAUSE\\TARPAUSE_US",

                // XBox only
                "GUI\\XBLIVE_FD\\XBLIVE_FD",
                "GUI\\XBLIVE_FD\\XBLPAUSE",
            };

            foreach (var guiFile in guiFiles)
                knownFiles.Add(String.Format("{0}.MEC", guiFile));

            var langs = new[] {
                "ENGLISH",
                "CHINESE",
                "TAIWANESE",
                "KOREAN",
                "JAPANESE",
                "SPANISH",
                "ITALIAN",
                "GERMAN",
                "FRENCH",
            };

            foreach (var lang in langs)
            {
                knownFiles.Add(String.Format("LOCALE\\{0}\\FONTS\\FONT.BNK", lang));

                foreach (var guiFile in guiFiles)
                    knownFiles.Add(String.Format("LOCALE\\{0}\\{1}.TXT", lang, guiFile));

                for (int i = 1; i <= 100; i++)
                    knownFiles.Add(String.Format("LOCALE\\{0}\\MISSIONS\\MISSION{1:D2}.TXT", lang, i));

                for (int i = 1; i <= 200; i++)
                {
                    knownFiles.Add(String.Format("LOCALE\\{0}\\FMV\\IGCS{1:D2}.TXT", lang, i));
                    knownFiles.Add(String.Format("LOCALE\\{0}\\MUSIC\\IGCS{1:D2}.XA", lang, i));
                }

                foreach (var fmv in fmvFiles)
                    knownFiles.Add(String.Format("LOCALE\\{0}\\{1}.TXT", lang, fmv));

                foreach (var sc in scFiles)
                    knownFiles.Add(String.Format("LOCALE\\{0}\\FMV\\SC{1}.TXT", lang, sc));

                knownFiles.Add(String.Format("LOCALE\\{0}\\TEXT\\CONTROLS.TXT", lang));
                knownFiles.Add(String.Format("LOCALE\\{0}\\TEXT\\GENERIC.TXT", lang));
                knownFiles.Add(String.Format("LOCALE\\{0}\\TEXT\\OVERLAYS.TXT", lang));

                knownFiles.Add(String.Format("LOCALE\\{0}\\SOUNDS\\SOUND.GSD", lang));
            }

            knownFiles.Sort();

            return knownFiles;
        }

        private List<String> HACK_CompileDriverPLFiles()
        {
            var knownFiles = new List<String>() {
                "TERRITORIES.TXT",

                "ANIMS\\NYC.AN4",
                "ANIMS\\NYC_NOW.AN4",

                "CHARACTERS\\NYC.SP",

                "CITIES\\AIEXPORT",
                "CITIES\\LEVELS.TXT",

                "FMV\\EXTRAS\\GPTRAILER.XMV",
                "FMV\\EXTRAS\\BOCRASHES.XMV",
                "FMV\\EXTRAS\\BOCHASES.XMV",
                "FMV\\EXTRAS\\INTERVIEW.XMV",

                "FONTS\\70S\\FONT.BNK",
                "FONTS\\90S\\FONT.BNK",

                "INPUT\\FRONTEND.TXT",
                "INPUT\\SIMULATION.TXT",
                "INPUT\\VISUALS.TXT",

                "MUSIC\\A.XA",
                "MUSIC\\B.XA",
                "MUSIC\\C.XA",
                "MUSIC\\CITY.XA",
                "MUSIC\\D.XA",
                "MUSIC\\E.XA",
                "MUSIC\\F.XA",
                "MUSIC\\G.XA",
                "MUSIC\\H.XA",

                "SFX\\ENVIRONMENT_MAP.PMU",
                "SFX\\PARTICLEEFFECTS.PPX",
                "SFX\\RENDERER.PMU",

                "VEHICLES\\NYC.VGT",
                "VEHICLES\\NYC_VEHICLES.SP",

                "VEHICLES\\TEST_NYC_VEHICLES.SP",

                "VEHICLES\\VEHICLES.TXT",
                "VEHICLES\\VVARS.TXT",
            };

            var igcsFiles = new[] {
                "MUSIC\\01_2_TUT_NAG_01A",
                "MUSIC\\01_2_TUT_NAG_01B",
                "MUSIC\\01_2_TUT_NAG_01C",

                "MUSIC\\01_2_TUT_NAG_02A",
                "MUSIC\\01_2_TUT_NAG_02B",
                "MUSIC\\01_2_TUT_NAG_02C",
            };

            var guiFiles = new[] {
                "GUI\\DEV",
                "GUI\\FRONT",
                "GUI\\FRONTEND",
                "GUI\\OPTIONS",

                "GUI\\PAUSE70S",
                "GUI\\PAUSE90S",

                "GUI\\TRPAUSE70S",

                "GUI\\VETUT70S",

                "GUI\\VEGARAGE70S",
                "GUI\\VEGARAGE90S",
            };

            var platforms = new[] {
                "PS2",
                "XBOX",
            };

            var eras = new[] {
                "NOW",
                "THEN",
            };

            var territories = new[] {
                "AMERICAS",
                "ASIA",
                "JAPAN",
                "EUROPE",
            };

            var langs = new[] {
                "ENGLISH",
                "CHINESE",
                "KOREAN",
                "JAPANESE",
                "SPANISH",
                "ITALIAN",
                "FRENCH",
                "GERMAN",
                "PORTUGUESE",
            };

            var moods = new[] {
                "DAY",
                "NIGHT",
            };
            
            foreach (var era in eras)
            {
                foreach (var platform in platforms)
                    knownFiles.Add(String.Format("CITIES\\NYC_{0}_{1}.D4C", era, platform));

                knownFiles.Add(String.Format("LIFEEVENTS\\NYC_{0}_MISSIONS.SP", era));
                knownFiles.Add(String.Format("LITTER\\{0}\\LITTER.D4L", era));

                int m = 1;
                int mEnd = 5;

                foreach (var mood in moods)
                {
                    while (m <= mEnd)
                    {
                        knownFiles.Add(String.Format("MOODS\\{0:D2}_{1}_{2}_BEGINNING_MOOD.TXT", m, era, mood));
                        knownFiles.Add(String.Format("MOODS\\{0:D2}_{1}_{2}_EARLY_MOOD.TXT", m, era, mood));
                        knownFiles.Add(String.Format("MOODS\\{0:D2}_{1}_{2}_MOOD.TXT", m, era, mood));
                        knownFiles.Add(String.Format("MOODS\\{0:D2}_{1}_{2}_LATE_MOOD.TXT", m, era, mood));
                        knownFiles.Add(String.Format("MOODS\\{0:D2}_{1}_{2}_END_MOOD.TXT", m, era, mood));

                        ++m;
                    }

                    mEnd *= 2;
                }

                knownFiles.Add(String.Format("MOODS\\DAYNIGHTCYCLE_{0}.TXT", era));

                knownFiles.Add(String.Format("OVERLAYS\\HUD-{0}.BIN", era));
                knownFiles.Add(String.Format("OVERLAYS\\HUD-{0}.GFX", era));

                knownFiles.Add(String.Format("OVERLAYS\\NYC_{0}.MAP", era));

                knownFiles.Add(String.Format("SKIES\\{0}\\DYNAMIC_SKY_DOME.PKG", era));

                knownFiles.Add(String.Format("SOUNDS\\NYC_{0}.VSB", era));
            }

            foreach (var terr in territories)
            {
                knownFiles.Add(String.Format("TERRITORY\\{0}\\GAMECONFIG.TXT", terr));

                knownFiles.Add(String.Format("TERRITORY\\{0}\\FMV\\LEGAL.XMV", terr));

                foreach (var guiFile in guiFiles)
                {
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\{1}.MEC", terr, guiFile));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\{1}.MEM", terr, guiFile));
                }

                foreach (var lang in langs)
                {
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\FONTS\\FONT.BNK", terr, lang));

                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\FONTS\\70S\\FONT.BNK", terr, lang));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\FONTS\\90S\\FONT.BNK", terr, lang));

                    foreach (var guiFile in guiFiles)
                        knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\{2}.TXT", terr, lang, guiFile));

                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\SOUNDS\\CHRSOUND.DAT", terr, lang));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\SOUNDS\\SOUND.GSD", terr, lang));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\SOUNDS\\SOUND.SP", terr, lang));

                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\TEXT\\CONTROLS.TXT", terr, lang));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\TEXT\\GENERIC.TXT", terr, lang));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\TEXT\\NETWORK.TXT", terr, lang));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\TEXT\\OVERLAYS.TXT", terr, lang));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\TEXT\\VEHNAMES.TXT", terr, lang));
                    
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\LIFEEVENTS\\NYC_NOW_MISSION_TEXT.SP", terr, lang));
                    knownFiles.Add(String.Format("TERRITORY\\{0}\\LOCALE\\{1}\\LIFEEVENTS\\NYC_THEN_MISSION_TEXT.SP", terr, lang));
                }
            }

            knownFiles.Sort();

            return knownFiles;
        }
        
        private bool _hashLookupCompiled = false;

        private bool HACK_CompileHashLookup()
        {
            // run once
            if (_hashLookupCompiled)
                return true;
            
            // will print only once
            if (Version == IMGVersion.PSP)
            {
                Program.WriteVerbose("WARNING: PSP archive uses unknown hashing method, cannot determine filenames!");
                return false;
            }

            // temporary hack
            var knownFiles = new List<String>() {
                "GAMECONFIG.TXT",

                "SYSTEM.CNF",

                "FMV\\ATARI.XMV",
                "FMV\\CREDITS.XMV",

                "FONTS\\FONT.BNK",

                "OVERLAYS\\LOADING.GFX",
                "OVERLAYS\\WHITE.GFX",
                
                "SFX\\SFX.PMU",
                
                "SOUNDS\\GAMEDATA.DAT",
                "SOUNDS\\MENU.DAT",
            };

            // Music
            for (int i = 1; i <= 100; i++)
                knownFiles.Add(String.Format("MUSIC\\{0:D2}.XA", i));

            knownFiles.AddRange(HACK_CompileDriv3rFiles());
            knownFiles.AddRange(HACK_CompileDriverPLFiles());
            
            var sb = new StringBuilder();

            foreach (var entry in knownFiles)
            {
                var name = entry.ToUpper();
                var key = CustomHasher.GetFilenameCRC(name);

                if (HashLookup.ContainsKey(key))
                {
                    if (HashLookup[key] != name)
                    {
                        Program.WriteVerbose("WARNING: Hash conflict for '{0}' -> '{1}'", name, HashLookup[key]);
                        continue;
                    }

                    Program.WriteVerbose("WARNING: Skipping duplicate entry '{0}'", name);
                    continue;
                }

                sb.AppendLine(name);
                HashLookup.Add(key, name);
            }

            File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "out_files.txt"), sb.ToString());

            _hashLookupCompiled = true;
            return true;
        }

        private string GetFileNameFromHash(uint filenameHash)
        {
            if (HACK_CompileHashLookup() && HashLookup.ContainsKey(filenameHash))
            {
                var name = HashLookup[filenameHash];
                return name;
            }

            return null;
        }

        private void SetEntryFileName(Entry entry, uint hash)
        {
            var filename = GetFileNameFromHash(hash);

            if (filename != null)
                entry.FileName = filename;
            else
                entry.FileNameHash = hash;
        }

        private byte[] GetDecryptedData(Stream stream, int version)
        {
            var length = (version > 2) ? stream.ReadInt32() : (Entries.Capacity * 0x38);
            var buffer = new byte[length];

            stream.Read(buffer, 0, length);

            byte decKey = 27;

            if (version > 2)
            {
                byte key = 21;

                for (int i = 0, offset = 1; i < length; i++, offset++)
                {
                    buffer[i] -= decKey;
                    decKey += key;
                    
                    if (offset > 6 && key == 21)
                    {
                        offset = 0;
                        key = 11;
                    }
                    if (key == 11 && offset > 24)
                    {
                        offset = 0;
                        key = 21;
                    }
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    buffer[i] -= decKey;
                    decKey += 11;
                }
            }
            
            return buffer;
        }
        
        private unsafe void ReadIMGEntries(Stream stream, int version)
        {
            var buffer = GetDecryptedData(stream, version);
            var count = Entries.Capacity;

            switch (version)
            {
            case 2:
                {
                    for (int i = 0; i < count; i++)
                    {
                        var entry = new Entry();

                        fixed (byte* p = buffer)
                        {
                            var ptr = (byte*)(p + (i * 0x38));

                            entry.FileName = new String((sbyte*)p);
                            entry.Offset = *(int*)(ptr + 0x30);
                            entry.Length = *(int*)(ptr + 0x34);
                        }

                        Entries.Add(entry);
                    }
                }
                break;
            case 3:
                {
                    for (int i = 0; i < count; i++)
                    {
                        var entry = new Entry();

                        fixed (byte* p = buffer)
                        {
                            var ptr = (byte*)(p + (i * 0xC));

                            entry.FileName = new String((sbyte*)(p + *(int*)(ptr)));
                            entry.Offset = *(int*)(ptr + 0x4);
                            entry.Length = *(int*)(ptr + 0x8);
                        }

                        Entries.Add(entry);
                    }
                }
                break;
            case 4:
                {
                    // icky hack
                    if (stream.Length < 0x8000)
                        goto IMG4_XBOX;

                    for (int i = 0; i < count; i++)
                    {
                        var entry = new Entry();

                        fixed (byte* p = buffer)
                        {
                            var ptr = (byte*)(p + (i * 0xC));
                            var hash = *(uint*)ptr;

                            SetEntryFileName(entry, hash);

                            entry.Offset = *(int*)(ptr + 0x4);
                            entry.Length = *(int*)(ptr + 0x8);
                        }

                        Entries.Add(entry);
                    }
                }
                break;
                IMG4_XBOX:
                {
                    for (int i = 0; i < count; i++)
                    {
                        var entry = new XBoxEntry();

                        fixed (byte* p = buffer)
                        {
                            var ptr = (byte*)(p + (i * 0xC));
                            var hash = *(uint*)ptr;

                            SetEntryFileName(entry, hash);

                            var data = *(int*)(ptr + 0x4);

                            entry.LumpIndex = data & 0xFF;
                            entry.Offset = (data >> 8) & 0xFFFFFF;

                            entry.Length = *(int*)(ptr + 0x8);
                        }

                        Entries.Add(entry);
                    }
                }
                break;
            }
        }
        
        private void LoadFile(string filename)
        {
            using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var type = fs.ReadInt32();

                var version = -1;
                var count = 0;
                
                if ((type & 0xFFFFFF) == 0x474D49)
                {
                    version = ((type >> 24) & 0xF);
                    Version = (version >= 2 && version <= 4) ? (IMGVersion)version : IMGVersion.Unknown;

                    if (Version == IMGVersion.Unknown)
                        throw new Exception("Failed to load IMG file - unsupported version!");

                    count = fs.ReadInt32();
                    Reserved = fs.ReadInt32();
                }
                else
                {
                    // PSP archive?
                    count = type;

                    for (int i = 0; i < 3; i++)
                    {
                        if (fs.ReadInt32() != count)
                        {
                            // nope
                            count = 0;
                            break;
                        }
                    }
                    
                    Version = IMGVersion.PSP;
                    version = (int)Version;
                }

                if (version == -1)
                    throw new Exception("File is not an IMG archive!");
                
                Entries = new List<Entry>(count);
                
                if (Version == IMGVersion.PSP)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var entry = new PSPEntry();

                        var hash = fs.ReadUInt32();

                        SetEntryFileName(entry, hash);

                        entry.Offset = fs.ReadInt32();
                        entry.Length = fs.ReadInt32();
                        entry.UncompressedLength = fs.ReadInt32();

                        Entries.Add(entry);
                    }
                }
                else
                {
                    ReadIMGEntries(fs, (int)Version);
                }

                IsLoaded = true;
            }
        }

        public static bool LoadLookupTable(string lookupTable)
        {
            if (!File.Exists(lookupTable))
                Console.WriteLine("WARNING: The lookup table was not found. All files will have the extension '.bin'!");

            using (var sr = new StreamReader(File.Open(lookupTable, FileMode.Open, FileAccess.Read)))
            {
                var splitStr = new[] { "0x", "[", "]", "=", "\"" };

                if (sr.ReadLine() == "# Magic number lookup file")
                {
                    int lineNum = 1;
                    string line = "";

                    while (!sr.EndOfStream)
                    {
                        ++lineNum;

                        if (String.IsNullOrEmpty((line = sr.ReadLine())))
                            continue;

                        // Skip comments
                        if (line.StartsWith("#!"))
                        {
                            // multi-line comments
                            while (!sr.EndOfStream)
                            {
                                line = sr.ReadLine();

                                ++lineNum;

                                if (line.StartsWith("!#"))
                                    break;
                            }

                            continue;
                        }
                        else if (line.StartsWith("#"))
                            continue;

                        var strAry = line.Split(splitStr, StringSplitOptions.RemoveEmptyEntries);

                        // key, val, comment
                        if (strAry.Length < 2)
                        {
                            Console.WriteLine("ERROR: An error occurred while parsing the lookup table.");
                            Console.WriteLine("Line {0}: {1}", lineNum, line);
                            return false;
                        }

                        uint key = 0;
                        string val = strAry[1];

                        // add little-endian lookup
                        if (line.StartsWith("0x", true, null))
                        {
                            key = uint.Parse(strAry[0], NumberStyles.AllowHexSpecifier);
                        }
                        else if (line.StartsWith("["))
                        {
                            key = BitConverter.ToUInt32(Encoding.UTF8.GetBytes(strAry[0]), 0);
                        }
                        else
                        {
                            Console.WriteLine("ERROR: An error occurred while parsing the lookup table.");
                            Console.WriteLine("Line {0}: {1}", lineNum, line);
                            return false;
                        }

                        if (!LookupTable.ContainsKey(key))
                        {
                            Program.WriteVerbose("Adding [0x{0:X8}, {1}] to lookup table.", key, val);
                            LookupTable.Add(key, val);
                        }
                        else
                        {
                            Console.WriteLine("WARNING: Duplicate entry in lookup table. Skipping.");
                            Console.WriteLine("Line {0}: {1}\r\n", lineNum, line);
                        }
                    }

                    return true;
                }
                else
                {
                    Console.WriteLine("ERROR: The specified lookup table cannot be used.");
                    return false;
                }
            }
        }

        private static string GetLumpFilename(string imgFile, XBoxEntry entry)
        {
            return String.Format("{0}.L{1:D2}", Path.GetFileNameWithoutExtension(imgFile), entry.LumpIndex).ToUpper();
        }

        public static void Unpack(string filename, string outputDir)
        {
            var img = new IMGArchive();

            img.LoadFile(filename);

            var sb = new StringBuilder();
            var filesDir = Path.Combine(outputDir, "Files");

            if (!Program.ListOnly && !Directory.Exists(filesDir))
                Directory.CreateDirectory(filesDir);

            int maxSize = 0x2C000000;

            if (!Program.ListOnly)
            {
                Console.WriteLine("Unpacking files...");

                if (!Program.VerboseLog)
                    Console.Write("Progress: ");
            }
            
            var cL = Console.CursorLeft;
            var cT = Console.CursorTop;

            var nSkip = 0;
            var idx = 1;

            var lump = -1;
            var lumpFilename = "";

            Stream stream = null;

            var fileDescriptors = new List<FileDescriptor>();

            for (int i = 0; i < img.Entries.Count; i++)
            {
                var entry = img.Entries[i];

                if (!Program.VerboseLog)
                {
                    Console.SetCursorPosition(cL, cT);
                    Console.Write("{0} / {1}", idx, img.Entries.Count);
                }

                if (entry is XBoxEntry)
                {
                    var xbEntry = entry as XBoxEntry;

                    if (lump != xbEntry.LumpIndex)
                    {
                        if (stream != null)
                            stream.Dispose();

                        lump = xbEntry.LumpIndex;
                        lumpFilename = GetLumpFilename(filename, xbEntry);
                        
                        stream = File.Open(Path.Combine(Path.GetDirectoryName(filename), lumpFilename), FileMode.Open, FileAccess.Read, FileShare.Read);
                    }

                }
                else if (stream == null && lump == -1)
                    stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                stream.Position = entry.FileOffset;

                string name = "";
                string ext = "bin";

                long length = entry.Length;
                
                if (!entry.HasFileName)
                {
                    var magic32 = stream.PeekUInt32();
                    var magic16 = (magic32 & 0xFFFF);

                    if (magic16 != 0xFEFF)
                    {
                        if (LookupTable.ContainsKey(magic32))
                            ext = LookupTable[magic32];
                    }
                    else
                    {
                        // assume unicode text file
                        ext = "txt";
                    }

                    var holdPos = stream.Position;

                    // TODO: move this to its own function
                    // (the while loop is so we don't have deeply nested if's)
                    while (ext == "bin")
                    {
                        // check for embedded .PSP archives
                        // (assuming the higher 16-bits aren't used!)
                        if (((magic32 >> 16) & 0xFFFF) == 0)
                        {
                            var isPSPFile = true;

                            for (int l = 0; l < 3; l++)
                            {
                                if (stream.ReadInt32() != magic32)
                                {
                                    isPSPFile = false;
                                    break;
                                }
                            }

                            if (isPSPFile)
                            {
                                ext = "psp";
                                break;
                            }
                        }

                        // check for xbox video
                        stream.Position = holdPos + 0xC;

                        if (stream.ReadInt32() == 0x58626F78)
                        {
                            // xbox video
                            ext = "xmv";
                            break;
                        }

                        // check for stuff we can identify by the EOF
                        stream.Position = holdPos + (length - 4);

                        var eof = stream.ReadInt32();

                        // recording file? (won't always work)
                        if (eof == 0x21524E4A)
                        {
                            ext = "pad";
                            break;
                        }
                        
                        // text file with a newline @ the end?
                        if ((((eof >> 16) & 0xFFFF) == 0x0A0D) || (((eof >> 8) & 0xFFFF) == 0x0A0D) || ((eof & 0xFFFF) == 0x0D0A))
                        {
                            ext = "txt";
                            break;
                        }

                        // possibly a TGA file w/ footer?
                        if (eof == 0x2E454C)
                        {
                            // verify the footer
                            stream.Position = holdPos + (length - 0x12);

                            if (stream.ReadString(16) == "TRUEVISION-XFILE")
                            {
                                ext = "tga";
                                break;
                            }
                        }

                        // unknown format :(
                        stream.Position = holdPos;
                        break; // BREAK THE LOOP!!!
                    }

                    if (entry is XBoxEntry)
                    {
                        name = String.Format("_UNKNOWN\\L{0:D2}\\{1}.{2}", (entry as XBoxEntry).LumpIndex, entry.FileName, ext);
                    }
                    else if (entry is PSPEntry)
                    {
                        name = String.Format("_UNKNOWN\\{0:D4}_{1:D4}_{2}.{3}",
                            ((entry.FileNameHash >> 24) & 0xFF), ((entry.FileNameHash >> 16) & 0xFF), (entry.FileNameHash & 0xFFFF), ext);
                    }
                    else
                    {
                        name = String.Format("_UNKNOWN\\{0}.{1}", entry.FileName, ext);
                    }                    
                }
                else
                {
                    name = entry.FileName;
                    ext = Path.GetExtension(name);

                    // '.ext' -> 'ext' (if applicable)
                    if (!String.IsNullOrEmpty(ext))
                        ext = ext.Substring(1).ToLower();
                }

                if (entry is XBoxEntry)
                {
                    sb.AppendFormat("0x{0:X8}, 0x{1:X8}, {2} -> {3}\r\n",
                        entry.FileOffset, entry.Length,
                        GetLumpFilename(filename, entry as XBoxEntry), name);
                }
                else if (entry is PSPEntry)
                {
                    sb.AppendFormat("0x{0:X8}, 0x{1:X8}, 0x{2:X8}, 0x{3:X8} -> {4}\r\n",
                        entry.FileOffset, entry.Length, ((PSPEntry)entry).UncompressedLength, entry.FileNameHash, name);
                }
                else
                {
                    sb.AppendFormat("0x{0:X8}, 0x{1:X8}, {2}\r\n",
                        entry.FileOffset, entry.Length, name);
                }
                
                // do not write any data if it's only a listing
                if (Program.ListOnly)
                    continue;

                // add file descriptor
                fileDescriptors.Add(new FileDescriptor() {
                    EntryData = entry,
                    FileName = name,

                    Index = i,
                });

                if (Program.NoFMV && ((ext == "xmv") || (ext == "xav")))
                {
                    Program.WriteVerbose("{0}: SKIPPING -> {1}", idx++, name);
                    nSkip++;

                    continue;
                }
                
                var nDir = Path.GetDirectoryName(name);

                if (!String.IsNullOrEmpty(nDir))
                {
                    var eDir = Path.Combine(filesDir, Path.GetDirectoryName(name));

                    if (!Directory.Exists(eDir))
                        Directory.CreateDirectory(eDir);
                }

                var outName = Path.Combine(filesDir, name);

                Program.WriteVerbose("{0}: {1}", idx++, name);

                if (!Program.Overwrite && File.Exists(outName))
                    continue;

                stream.Position = entry.FileOffset;

                if (length < maxSize)
                {
                    var buffer = new byte[length];
                    stream.Read(buffer, 0, buffer.Length);

                    File.WriteAllBytes(outName, buffer);
                }
                else if (length > maxSize)
                {
                    int splitSize = (int)(length - maxSize);

                    using (var file = File.Create(outName))
                    {
                        for (int w = 0; w < maxSize; w += 0x100000)
                            file.Write(stream.ReadBytes(0x100000));

                        file.Write(stream.ReadBytes(splitSize));
                    }
                }
            }

            if (sb.Length > 0)
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(filename), "files.txt"), sb.ToString());

            stream.Dispose();

            if (!Program.ListOnly)
            {
                // clear old data
                sb.Clear();

                // build archive config
                sb.AppendLine($"archive {Path.GetFileName(filename)}");
                sb.AppendLine($"type {(int)img.Version}");
                sb.AppendLine($"version 1");

                sb.AppendLine();

                var dataIndex = 0;
                var useSorting = false; // if Index != DataIndex for any entries

                // add data index to descriptors
                foreach (var descriptor in fileDescriptors.OrderBy((e) => e.EntryData.FileOffset))
                {
                    // fixup the data index
                    descriptor.DataIndex = dataIndex++;

                    if (!useSorting && (descriptor.Index != descriptor.DataIndex))
                        useSorting = true;
                }

                // write data descriptors by their index (implicitly)
                foreach (var descriptor in fileDescriptors)
                {
                    if (useSorting)
                        sb.Append($"{descriptor.DataIndex:D4},");

                    sb.AppendLine($"0x{descriptor.EntryData.FileNameHash:X8},{descriptor.FileName}");
                }

                // no empty line at the end :)
                sb.Append("#EOF");
                
                // write the archive config
                File.WriteAllText(Path.Combine(outputDir, Path.GetDirectoryName(filename), "archive.cfg"), sb.ToString());

                Console.WriteLine((!Program.VerboseLog) ? "\r\n" : "");
                Console.WriteLine("Unpacked {0} files.", (img.Entries.Count - nSkip));

                if (nSkip > 0)
                    Console.WriteLine("{0} files skipped.", nSkip);
            }
        }

        public static void BuildArchive(string configFile, string outputDir)
        {
            var inputDir = Path.GetDirectoryName(configFile);
            var filesDir = Path.Combine(inputDir, "Files");

            Console.SetBufferSize(Console.BufferWidth, 4000);

            var blacklistFile = Path.Combine(inputDir, "blacklist.txt");
            var blacklist = new List<string>();
            
            if (File.Exists(blacklistFile))
            {
                Program.WriteVerbose($"Loading blacklist: '{blacklistFile}'");

                using (var sr = new StreamReader(blacklistFile, true))
                {
                    var line = 0;

                    while (!sr.EndOfStream)
                    {
                        var curLine = sr.ReadLine();
                        ++line;

                        if (String.IsNullOrEmpty(curLine))
                            continue;

                        var blEntry = Path.Combine(filesDir, curLine);
                        
                        if (!File.Exists(blEntry))
                        {
                            Program.WriteVerbose($"Skipping invalid blacklist entry on line {line}: '{curLine}'");
                            continue;
                        }

                        blacklist.Add(curLine);
                    }
                }

                if (blacklist.Count > 0)
                    Program.WriteVerbose($"Loaded {blacklist.Count} blacklist entries.");
                else
                    Program.WriteVerbose("Blacklist is empty, no entries added.");
            }
            
            using (var sr = new StreamReader(configFile, true))
            {
                var archive = "";

                var type = -1;
                var version = -1;

                var files = new List<FileDescriptor>();
                var numFiles = 0;
                
                var step = 0;
                var inHeader = true;

                var line = 0;

                Console.WriteLine("Parsing archive configuration...");

                while (!sr.EndOfStream)
                {
                    var curLine = sr.ReadLine();
                    
                    ++line;

                    // EOF?
                    if (String.Equals(curLine, "#EOF", StringComparison.InvariantCultureIgnoreCase))
                        break;
                    
                    // skip empty lines/comments
                    if (String.IsNullOrEmpty(curLine) || curLine.StartsWith("#"))
                        continue;

                    string[] input = { };

                    // read header
                    if (step < 3)
                    {
                        input = curLine.Split(' ');

                        var keyword = input[0].ToLower();

                        if (input.Length < 2)
                        {
                            Program.WriteVerbose($"Unknown input '{keyword}', skipping...");
                            continue;
                        }

                        // move past keyword + space
                        var value = curLine.Substring(keyword.Length + 1);

                        switch (keyword)
                        {
                        case "archive":
                            archive = value;
                            break;
                        case "type":
                            if (!int.TryParse(value, out type))
                                throw new InvalidOperationException($"Invalid archive type '{value}', must be an integer.");
                            break;
                        case "version":
                            if (!int.TryParse(value, out version))
                                throw new InvalidOperationException($"Invalid config. version '{version}', must be an integer.");
                            break;
                        default:
                            // rip
                            throw new InvalidOperationException($"Invalid archive configuration: unknown data '{curLine}'.");
                        }

                        // read next header entry
                        ++step;
                        continue;
                    }

                    if (inHeader)
                    {
                        // verify we got the info. needed
                        if (String.IsNullOrEmpty(archive))
                            throw new InvalidOperationException("Invalid archive configuration: missing/empty 'archive' data!");
                        if (type == -1)
                            throw new InvalidOperationException("Invalid archive configuration: missing 'type' data!");
                        if (version == -1)
                            throw new InvalidOperationException("Invalid archive configuration: missing 'version' data!");

                        if ((version < 1) || (version > 1))
                            throw new InvalidOperationException($"Invalid archive configuration: unknown version '{version}'.");

                        // end of header
                        inHeader = false;
                    }

                    // read file entries
                    input = curLine.Split(',');

                    switch ((IMGVersion)type)
                    {
                    case IMGVersion.IMG2:
                    case IMGVersion.IMG3:
                    case IMGVersion.IMG4:
                        throw new NotSupportedException($"Archive configuration 'IMG{type}' currently unsupported.");
                    case IMGVersion.PSP:
                        {
                            if (input.Length != 3)
                                throw new InvalidOperationException($"Invalid archive configuration: malformed entry @ line {line}.");

                            int dataIndex = -1;
                            uint hash = 0;

                            if (!int.TryParse(input[0], NumberStyles.Integer, CultureInfo.CurrentCulture, out dataIndex))
                                throw new InvalidOperationException($"Invalid archive configuration: entry @ line {line} has invalid data index ({input[0]}).");

                            if (!uint.TryParse(input[1].Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out hash))
                                throw new InvalidOperationException($"Invalid archive configuration: entry @ line {line} has invalid hash ({input[1]}).");

                            var filename = input[2];

                            if (String.IsNullOrEmpty(filename))
                                throw new InvalidOperationException($"Invalid archive configuration: entry @ line {line} has empty filename.");

                            if (blacklist.Contains(filename))
                            {
                                Program.WriteVerbose($"Skipping blacklisted entry on line {line}: '{filename}'");
                                continue;
                            }
                            else
                            {
                                files.Add(new FileDescriptor() {
                                    Index = numFiles,
                                    DataIndex = dataIndex,

                                    FileName = filename,

                                    EntryData = new PSPEntry() {
                                        FileNameHash = hash,
                                    },
                                });
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"Invalid archive configuration: invalid type '{type}'!");
                    }

                    // file added successfully
                    ++numFiles;
                }

                Console.WriteLine($"{files.Count} files processed.");
                Console.WriteLine("Calculating buffer size...");
                
                var bufferSize = 0L;
                
                // calculate the size of the file and fill in the entry data
                switch ((IMGVersion)type)
                {
                case IMGVersion.IMG2:
                case IMGVersion.IMG3:
                case IMGVersion.IMG4:
                    throw new NotSupportedException($"Archive configuration 'IMG{type}' currently unsupported.");
                case IMGVersion.PSP:
                    {
                        // header size + entries
                        bufferSize += (0x10 + (numFiles * 0x10));
                        
                        foreach (var entry in files.OrderBy((f) => f.DataIndex))
                        {
                            var entryData = (entry.EntryData as PSPEntry);

                            var filename = Path.Combine(filesDir, entry.FileName);

                            if (!File.Exists(filename))
                            {
                                Program.WriteVerbose($"WARNING: File '{filename}' is missing, skipping...");
                                continue;
                            }

                            var fileInfo = new FileInfo(filename);

                            bufferSize = MemoryHelper.Align(bufferSize, 2048);

                            if (bufferSize > int.MaxValue)
                                throw new InvalidOperationException("Archive filesize limit exceeded! Trim some files and try again.");

                            entryData.Offset = (int)bufferSize;
                            entryData.Length = (int)fileInfo.Length;
                            entryData.UncompressedLength = (int)fileInfo.Length;
                            
                            bufferSize += fileInfo.Length;
                        }

                        bufferSize = MemoryHelper.Align(bufferSize, 2048);
                    } break;
                }

                Console.WriteLine("Building archive...please wait");

                if (!Program.VerboseLog)
                    Console.Write("Progress: ");
                
                var cL = Console.CursorLeft;
                var cT = Console.CursorTop;

                // let's get to work!
                var archiveDir = Path.Combine(outputDir, "Build");
                var archiveFileName = Path.Combine(archiveDir, archive);

                if (!Directory.Exists(archiveDir))
                    Directory.CreateDirectory(archiveDir);

                using (var fs = new FileStream(archiveFileName, FileMode.Create, FileAccess.Write, FileShare.Read, (1024 * 1024), FileOptions.RandomAccess))
                {
                    fs.SetLength(bufferSize);

                    switch ((IMGVersion)type)
                    {
                    case IMGVersion.IMG2:
                    case IMGVersion.IMG3:
                    case IMGVersion.IMG4:
                        throw new NotSupportedException($"Archive configuration 'IMG{type}' currently unsupported.");
                    case IMGVersion.PSP:
                        {
                            // write number of files 4 times
                            for (int n = 0; n < 4; n++)
                                fs.Write(numFiles);

                            // write each entry in the header and also its data
                            for (int i = 0, idx = 1; i < numFiles; i++, idx++)
                            {
                                var entry = files[i];
                                var entryData = (entry.EntryData as PSPEntry);

                                var filename = Path.Combine(filesDir, entry.FileName);

                                fs.Position = (0x10 + (i * 0x10));

                                fs.Write(entryData.FileNameHash);
                                fs.Write(entryData.Offset);
                                fs.Write(entryData.Length);
                                fs.Write(entryData.UncompressedLength);

                                if (!Program.VerboseLog)
                                {
                                    Console.SetCursorPosition(cL, cT);
                                    Console.Write("{0} / {1}", idx, numFiles);
                                }

                                Program.WriteVerbose($"[{idx:D4}] {entry.FileName} @ {entryData.Offset:X8} (size: {entryData.Length:X8})");
                                
                                using (var ffs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, entryData.Length, FileOptions.SequentialScan))
                                {
                                    if (ffs.Length != entryData.Length)
                                        throw new InvalidOperationException($"FATAL ERROR: File size for '{entry.FileName}' was changed before the archive could be built!");

                                    // seek to the file offset and write all data
                                    fs.Position = entryData.FileOffset;

                                    ffs.CopyTo(fs, entryData.Length);
                                }
                            }
                        }
                        break;
                    }
                }

                if (!Program.VerboseLog)
                    Console.WriteLine("\r\n");

                Console.WriteLine($"Successfully saved archive '{archive}' to '{archiveDir}'!");
            }
        }

        public void SaveFile(string filename)
        {
            // maybe!
            throw new NotImplementedException();
        }
    }
    
}
