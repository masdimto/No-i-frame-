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
        public override Version Version => new Version(1, 3, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "True Multiplayer 0 I-Frame: Memotong HP pemain secara paksa via paket PlayerHp & mereset i-frame musuh/bos.";

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

            // 2. Reset i-frame PEMAIN & Paksa Potong HP saat menyentuh hitbox musuh/proyektil
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

                Rectangle playerHitbox = player.Hitbox;
                int rawDamage = 0;
                string killerName = "Musuh";

                // Cek sentuhan dengan badan musuh / bos
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

                // Cek sentuhan dengan proyektil musuh / bos
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
                    contactTicks[i]++;

                    // Tick 1 dibiarkan dihitung HP pemain; mulai Tick 2 dst server langsung memotong HP tanpa peduli i-frame!
                    if (contactTicks[i] >= 2)
                    {
                        // Hitung pengurangan damage berdasarkan Defense pemain (Normal 0.5x, Expert 0.75x, Master 1.0x)
                        float defFactor = Main.masterMode ? 1.0f : (Main.expertMode ? 0.75f : 0.5f);
                        int finalDamage = Math.Max(1, (int)(rawDamage - (player.statDefense * defFactor)));

                        // Kurangi Endurance (misal Worm Scarf / Endurance Potion)
                        if (player.endurance > 0f)
                        {
                            finalDamage = Math.Max(1, (int)(finalDamage * (1f - player.endurance)));
                        }

                        player.statLife -= finalDamage;

                        // Tampilkan angka merah damage di atas kepala pemain
                        CombatText.NewText(playerHitbox, CombatText.DamagedFriendly, finalDamage, false, true);

                        if (player.statLife <= 0)
                        {
                            player.statLife = 0;
                            NetMessage.SendData((int)PacketTypes.PlayerHp, -1, -1, null, i);
                            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason($"{player.name} tewas seketika oleh {killerName} (0 I-Frame)!");
                            tsPlayer.KillPlayer();
                        }
                        else
                        {
                            // Paksa kirim update HP terbaru ke HP pemain (tidak bisa diblokir i-frame client!)
                            NetMessage.SendData((int)PacketTypes.PlayerHp, -1, -1, null, i);
                        }
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
