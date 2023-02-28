using BepInEx;
using EntityStates;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace RoR2TweaksMod
{
    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]

    //This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    //We will be using 2 modules from R2API: ItemAPI to add our item and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI), nameof(RecalculateStatsAPI))]

    //This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class Tweaks : BaseUnityPlugin
    {
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Haggleman";
        public const string PluginName = "Haggleman's Tweaks";
        public const string PluginVersion = "0.2.0";

        //We need our item definition to persist through our functions, and therefore make it a class field.
        private static ItemDef myItemDef;
        private static GameObject banner;

        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            Hooks();
            EditSkills();
            // This line of log will appear in the bepinex console when the Awake method is done.
            Log.LogInfo(nameof(Awake) + " done.");
        }


        private void Hooks()
        {
            AdjustFuelArrayDetonate();
            On.EntityStates.QuestVolatileBattery.CountDown.Detonate += SelfDamage;

            // Warbanner changes
            On.RoR2.Items.WardOnLevelManager.OnCharacterLevelUp += (_,_) => { };
            PreventBannerSpawn();
            On.RoR2.HoldoutZoneController.OnEnable += SpawnWarBanner;
            On.RoR2.HoldoutZoneController.OnDisable += DespawnWarBanner;
            On.RoR2.HoldoutZoneController.FixedUpdate += UpdateWarbanner;
            RecalculateStatsAPI.GetStatCoefficients += CalculateWarbannerBuff;

            // Commando Dodge-Roll
            On.EntityStates.Commando.DodgeState.OnEnter += AddInvuln;
            On.EntityStates.Commando.DodgeState.OnExit += RemoveInvuln;

            // Edit Projectiles
            On.RoR2.ProjectileCatalog.Init += EditProjectilePrefabs;

            // Edit Skills
            On.RoR2.CharacterBody.Start += EditSkillPrefabs;
        }

        private void EditSkills()
        {
            EditMortarRain();
        }

        private void EditSkillPrefabs(On.RoR2.CharacterBody.orig_Start orig, CharacterBody self)
        {
            orig(self);
            if (self.bodyIndex == BodyCatalog.FindBodyIndex("TreebotBody"))
            {
                Debug.Log("Treebot Found ---------------------------");
                //EditTreeBot(self);
            }
            else
            {
                Debug.Log("None Found ------------------------------");
            }
        }
        private void EditMortarRain()
        {
            GameObject TreebotBodyPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Treebot/TreebotBody.prefab").WaitForCompletion();
            SkillLocator skillLocator = TreebotBodyPrefab.GetComponent<SkillLocator>();
            RoR2.Skills.SkillFamily skillFamily = skillLocator.secondary.skillFamily;

            //string altSecondaryName = "TreebotBodyAimMortarRain";
            //int skillIndex = skillFamily.GetVariantIndex(altSecondaryName);
            int skillIndex = 0;
            RoR2.Skills.SkillDef rain = skillFamily.variants[skillIndex].skillDef;
            Debug.Log("Skill 0 Name: " + rain.skillName);

            //RoR2.Skills.SkillDef newRain = ScriptableObject.CreateInstance<RoR2.Skills.SkillDef>();
            RoR2.Skills.SkillDef newRain = rain;

            //newRain.activationState = rain.activationState;
            //newRain.activationStateMachineName = rain.activationStateMachineName;
            //newRain.baseMaxStock = rain.baseMaxStock;
            newRain.baseRechargeInterval = 4f;
            //newRain.beginSkillCooldownOnSkillEnd = rain.beginSkillCooldownOnSkillEnd;
            //newRain.canceledFromSprinting = rain.canceledFromSprinting;
            //newRain.cancelSprintingOnActivation = rain.cancelSprintingOnActivation;
            //newRain.fullRestockOnAssign = rain.fullRestockOnAssign;
            //newRain.interruptPriority = rain.interruptPriority;
            //newRain.isCombatSkill = rain.isCombatSkill;
            //newRain.mustKeyPress = rain.mustKeyPress;
            //newRain.rechargeStock = rain.rechargeStock;
            //newRain.requiredStock = rain.requiredStock;
            //newRain.stockToConsume = rain.stockToConsume;
            //newRain.icon = rain.icon;
            //newRain.skillDescriptionToken = rain.skillDescriptionToken;
            //newRain.skillName = rain.skillName;
            //newRain.skillNameToken = rain.skillNameToken;

            ContentAddition.AddSkillDef(newRain);


            //Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            //skillFamily.variants[skillFamily.variants.Length - 1] = new RoR2.Skills.SkillFamily.Variant
            skillFamily.variants[skillIndex] = new RoR2.Skills.SkillFamily.Variant
            {
                skillDef = newRain,
                unlockableDef = new UnlockableDef(),
                viewableNode = new ViewablesCatalog.Node(newRain.skillNameToken, false, null)
            };
        }

        private void EditProjectilePrefabs(On.RoR2.ProjectileCatalog.orig_Init orig)
        {
            orig();
            EditGrenade();
        }

        private void EditGrenade()
        {
            GameObject frag = ProjectileCatalog.GetProjectilePrefab(38);
            frag.GetComponent<ProjectileImpactExplosion>().lifetimeAfterImpact = 0.5f;
            frag.GetComponent<ProjectileSimple>().desiredForwardSpeed = 20.0f;
            frag.GetComponent<Rigidbody>().mass = 40.0f;
        }

        private void AddInvuln(On.EntityStates.Commando.DodgeState.orig_OnEnter orig, EntityStates.Commando.DodgeState self)
        {
            if (NetworkServer.active)
            {
                orig(self);
                self.characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
            }
        }
        private void RemoveInvuln(On.EntityStates.Commando.DodgeState.orig_OnExit orig, EntityStates.Commando.DodgeState self)
        {
            if (NetworkServer.active)
            {
                self.characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);
                orig(self);
            }
        }
        private void PreventBannerSpawn()
        {
            IL.RoR2.TeleporterInteraction.ChargingState.OnEnter += (il) =>
            {
                ILCursor c = new ILCursor(il);
                c.GotoNext(MoveType.After,
                    x => x.MatchLdloc(7),
                    x => x.MatchCallOrCallvirt<CharacterMaster>("get_inventory"),
                    x => x.MatchLdsfld(typeof(RoR2Content.Items),"WardOnLevel"),
                    x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                    x => x.MatchStloc(8)
                    );
                c.Index -= 1;
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_I4_0);
            };
        }
        private void SpawnWarBanner(On.RoR2.HoldoutZoneController.orig_OnEnable orig, HoldoutZoneController self)
        {
            Debug.Log(" SpawnWarBanner Called!");
            int bannerCount = Util.GetItemCountForTeam(self.chargingTeam, RoR2Content.Items.WardOnLevel.itemIndex, requiresAlive: false);
            Debug.Log(" " + self.chargingTeam + " " + bannerCount);
            if (NetworkServer.active)
            {
                if (bannerCount > 0)
                {
                    banner = Instantiate(LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/WarbannerWard"), self.transform.position, Quaternion.identity);
                    banner.GetComponent<TeamFilter>().teamIndex = self.chargingTeam;
                    banner.GetComponent<BuffWard>().Networkradius = self.currentRadius;
                    NetworkServer.Spawn(banner);
                }
            }
            orig(self);
        }
        private void DespawnWarBanner(On.RoR2.HoldoutZoneController.orig_OnDisable orig, HoldoutZoneController self)
        {
            NetworkServer.UnSpawn(banner);
            orig(self);
        }
        private void UpdateWarbanner(On.RoR2.HoldoutZoneController.orig_FixedUpdate orig, HoldoutZoneController self)
        {
            Debug.Log(" UpdateWarBanner Called!");
            Debug.Log(" Radius: " + self.currentRadius);
            banner.GetComponent<BuffWard>().Networkradius = self.currentRadius;
            orig(self);
        }
        private void CalculateWarbannerBuff(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.HasBuff(RoR2Content.Buffs.Warbanner))
            {
                int bannerCount = Util.GetItemCountForTeam(sender.teamComponent.teamIndex, RoR2Content.Items.WardOnLevel.itemIndex, requiresAlive: false);
                args.moveSpeedMultAdd += 0.15f * (bannerCount - 1);
                args.attackSpeedMultAdd += 0.15f * (bannerCount - 1);
            }
        }

        private void SelfDamage(On.EntityStates.QuestVolatileBattery.CountDown.orig_Detonate orig, EntityStates.QuestVolatileBattery.CountDown self)
        {
            if ((bool)self.networkedBodyAttachment.attachedBody.healthComponent)
            {
                DamageInfo damageInfo = new DamageInfo();
                damageInfo.damage = self.networkedBodyAttachment.attachedBody.healthComponent.combinedHealth * 3.0f;
                damageInfo.position = self.networkedBodyAttachment.attachedBody.corePosition;
                damageInfo.force = Vector3.zero;
                damageInfo.damageColorIndex = DamageColorIndex.Default;
                damageInfo.crit = false;
                damageInfo.attacker = self.networkedBodyAttachment.attachedBodyObject;
                damageInfo.inflictor = self.networkedBodyAttachment.gameObject;
                damageInfo.damageType = DamageType.Generic;
                damageInfo.procCoefficient = 0f;
                damageInfo.procChainMask = default(ProcChainMask);
                self.networkedBodyAttachment.attachedBody.healthComponent.TakeDamage(damageInfo);
            }
            orig(self);
        }

        private void AdjustFuelArrayDetonate()
        {
            IL.EntityStates.QuestVolatileBattery.CountDown.Detonate += (il) =>
            {
                ILCursor c = new ILCursor(il);
                c.GotoNext(MoveType.Before,
                    x => x.MatchDup(),
                    x => x.MatchLdcI4(1),
                    x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.attackerFiltering))
                    );
                c.Index += 1;
                c.Next.OpCode = OpCodes.Ldc_I4_2;
                c.GotoNext(MoveType.Before,
                    x => x.MatchDup(),
                    x => x.MatchLdcI4(0),
                    x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.crit))
                    );
                c.Index += 1;
                c.Next.OpCode = OpCodes.Ldc_I4_1;
                c.GotoNext(MoveType.Before,
                    x => x.MatchDup(),
                    x => x.MatchLdcR4(0.0f),
                    x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.procCoefficient))
                    );
                c.Index += 1;
                c.Next.Operand = 1.0f;
            };
        }
    }
}
