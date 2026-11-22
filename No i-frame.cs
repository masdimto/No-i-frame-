using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;

namespace NoIFramePlugin
{
    [ApiVersion(2, 1)]
    public class NoIFrame : TerrariaPlugin
    {
        public override string Name => "NoIFrame";
        public override Version Version => new Version(1, 2, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "True Multiplayer 0 I-Frame: Menghilangkan jeda kebal baik pada pemain (server-side hitbox) maupun musuh/bos.";

        // Melacak berapa tick pemain sudah menempel di hitbox musuh/proyektil
        private int[] contactTicks = new int[Main.maxPlayers];

        public NoIFrame(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
            }
            base.Dispose(disposing);
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            if (args.Who >= 0 && args.Who < Main.maxPlayers)
                contactTicks[args.Who] = 0;
        }

        private void OnGameUpdate(EventArgs args)
        {
            // 1. Reset semua i-frame pada MUSUH & BOS (100% server-side)
            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (npc == null || !npc.active || npc.friendly)
                    continue;

                for (int p = 0; p < Main.maxPlayers; p++)
                {
                    if (npc.immune[p] > 0)
                        npc.immune[p] = 0;
                }
            }

            // 2. Reset i-frame PEMAIN & Paksa Hitbox Damage Multiplayer
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                TSPlayer tsPlayer = TShock.Players[i];
                if (tsPlayer == null || !tsPlayer.Active || tsPlayer.TPlayer == null || tsPlayer.TPlayer.dead)
                {
                    contactTicks[i] = 0;
                    continue;
                }

                Player player = tsPlayer.TPlayer;

                if (player.immune || player.immuneTime > 0)
                {
                    player.immune = false;
                    player.immuneTime = 0;
                    player.immuneNoBlink = false;
                }

                for (int c = 0; c < player.hurtCooldowns.Length; c++)
                {
                    if (player.hurtCooldowns[c] > 0)
                        player.hurtCooldowns[c] = 0;
                }

                // Cek tabrakan aktif dengan Musuh atau Proyektil Hostile di server
                Rectangle playerHitbox = player.Hitbox;
                int incomingDamage = 0;
                string killerName = "Musuh";
                int hitDirection = 0;

                // Cek sentuhan badan musuh / bos
                for (int n = 0; n < Main.maxNPCs; n++)
                {
                    NPC npc = Main.npc[n];
                    if (npc != null && npc.active && !npc.friendly && npc.damage > 0 && !npc.dontTakeDamage)
                    {
                        if (playerHitbox.Intersects(npc.Hitbox))
                        {
                            incomingDamage = npc.damage;
                            killerName = npc.FullName;
                            hitDirection = player.Center.X < npc.Center.X ? -1 : 1;
                            break;
                        }
                    }
                }

                // Jika tidak menempel badan musuh, cek proyektil musuh/bos (laser, peluru, bola api)
                if (incomingDamage == 0)
                {
                    for (int p = 0; p < Main.maxProjectiles; p++)
                    {
                        Projectile proj = Main.projectile[p];
                        if (proj != null && proj.active && proj.hostile && proj.damage > 0)
                        {
                            if (playerHitbox.Intersects(proj.Hitbox))
                            {
                                incomingDamage = proj.damage * 2;
                                killerName = Lang.GetProjectileName(proj.type).Value;
                                if (string.IsNullOrWhiteSpace(killerName))
                                    killerName = "Serangan Musuh";
                                hitDirection = player.Center.X < proj.Center.X ? -1 : 1;
                                break;
                            }
                        }
                    }
                }

                if (incomingDamage > 0)
                {
                    contactTicks[i]++;

                    // Tick ke-1 dibiarkan dihitung HP pemain sendiri; mulai tick ke-3 dan kelipatan 2 server memaksa tembus i-frame
                    if (contactTicks[i] >= 3 && contactTicks[i] % 2 == 1)
                    {
                        PlayerDeathReason reason = PlayerDeathReason.ByCustomReason($"{player.name} tewas seketika oleh {killerName} (0 I-Frame)!");
                        NetMessage.SendPlayerHurt(
                            i,
                            reason,
                            incomingDamage,
                            hitDirection,
                            false,
                            false,
                            -1
                        );
                    }
                }
                else
                {
                    contactTicks[i] = 0;
                }
            }
        }
    }
}
