using System;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace NoIFrames
{
    [ApiVersion(2, 1)]
    public class NoIFramesPlugin : TerrariaPlugin
    {
        public override string Name => "NoIFrames";
        public override string Author => "You";
        public override string Description => "Removes invincibility frames from players, NPCs and projectiles.";
        public override Version Version => new Version(1, 1, 0);

        public NoIFramesPlugin(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose(disposing);
        }

        private void OnUpdate(EventArgs args)
        {
            // NPCs: clear per-player hit immunity so every hit can land.
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active)
                    continue;

                for (int p = 0; p < npc.immune.Length; p++)
                    npc.immune[p] = 0;
            }

            // Projectiles: clear local NPC hit cooldowns (piercing/multi-hit projectiles).
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (!proj.active)
                    continue;

                for (int n = 0; n < proj.localNPCImmunity.Length; n++)
                    proj.localNPCImmunity[n] = 0;
            }

            // Players: clear post-hit invincibility.
            foreach (TSPlayer plr in TShock.Players)
            {
                if (plr == null || !plr.Active)
                    continue;

                plr.TPlayer.immune = false;
                plr.TPlayer.immuneTime = 0;
            }
        }
    }
}
