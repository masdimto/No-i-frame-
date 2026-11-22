using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;

namespace NoIFramePlugin
{
    [ApiVersion(2, 1)]
    public class NoIFrame : TerrariaPlugin
    {
        public override string Name => "NoIFrame";
        public override Version Version => new Version(1, 4, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "True Multiplayer 0 I-Frame: Mengirim paket PlayerHurtV2 langsung tanpa jeda tick agar HP client berkurang real-time.";

        private int[] tickSkip = new int[Main.maxPlayers];

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
                tickSkip[args.Who] = 0;
        }

        private void OnGameUpdate(EventArgs args)
        {
            // 1. Reset semua i-frame pada MUSUH & BOS
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

            // 2. Reset i-frame PEMAIN & Kirim Paket PlayerHurtV2 Langsung
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                TSPlayer tsPlayer = TShock.Players[i];
                if (tsPlayer == null || !tsPlayer.Active || tsPlayer.TPlayer == null || tsPlayer.TPlayer.dead)
                    continue;

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

                // Jeda 2 tick (30 hit per detik) agar paket jaringan HP tidak macet
                tickSkip[i]++;
                if (tickSkip[i] < 2)
                    continue;
                tickSkip[i] = 0;

                Rectangle playerHitbox = player.Hitbox;
                int rawDamage = 0;
                string killerName = "Musuh";

                // Cek sentuhan badan musuh / bos
                for (int n = 0; n < Main.maxNPCs; n++)
                {
                    NPC npc = Main.npc[n];
                    if (npc != null && npc.active && !npc.friendly && npc.damage > 0 && !npc.dontTakeDamage)
                    {
                        if (playerHitbox.Intersects(npc.Hitbox))
                        {
                            rawDamage = npc.damage;
                            killerName = npc.FullName;
                            break;
                        }
                    }
                }

                // Cek sentuhan proyektil musuh / bos
                if (rawDamage == 0)
                {
                    for (int p = 0; p < Main.maxProjectiles; p++)
                    {
                        Projectile proj = Main.projectile[p];
                        if (proj != null && proj.active && proj.hostile && proj.damage > 0)
                        {
                            if (playerHitbox.Intersects(proj.Hitbox))
                            {
                                rawDamage = proj.damage * 2;
                                killerName = Lang.GetProjectileName(proj.type).Value;
                                if (string.IsNullOrWhiteSpace(killerName))
                                    killerName = "Serangan Musuh";
                                break;
                            }
                        }
                    }
                }

                if (rawDamage > 0)
                {
                    // Kirim paket resmi PlayerHurtV2 ke client (hitDirection = 0 agar tidak terpental)
                    PlayerDeathReason reason = PlayerDeathReason.ByCustomReason($"{player.name} tewas seketika oleh {killerName} (0 I-Frame)!");
                    NetMessage.SendPlayerHurt(
                        i,
                        reason,
                        rawDamage,
                        0,
                        false,
                        false,
                        -1
                    );
                }
            }
        }
    }
}
