﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using xTile.Dimensions;
using RectangleX = Microsoft.Xna.Framework.Rectangle;
using Rectangle = xTile.Dimensions.Rectangle;
using StardewValley.Objects;

namespace MapImageExporter
{
    public class Mod : StardewModdingAPI.Mod
    {
        public static Mod instance;
        private ConcurrentQueue<RenderQueueEntry> renderQueue = new ConcurrentQueue<RenderQueueEntry>();

        public override void Entry(IModHelper helper)
        {
            instance = this;

            Helper.ConsoleCommands.Add("export", "See 'export help'", exportCommand);
            GameEvents.UpdateTick += checkRenderQueue;
        }

        private void exportCommand( string str, string[] args )
        {
            if ( args.Length < 1 )
            {
                Log.error("No command/map_name given.");
                return;
            }

            if ( args[ 0 ] == "all" )
            {
                RenderFlags flags = RenderFlags.Tiles | RenderFlags.Location;
                if (args.Length == 2 && args[1] == "all")
                {
                    flags = (RenderFlags)0xFFFF;
                }
                else if ( args.Length > 1 )
                {
                    flags = RenderFlags.None;
                    for ( int i = 1; i < args.Length; ++i )
                    {
                        switch ( args[ i ] )
                        {
                            case "tiles"    : flags |= RenderFlags.Tiles;           break;
                            case "light"    : flags |= RenderFlags.Lighting;        break;
                            case "npcs"     : flags |= RenderFlags.Characters;      break;
                            case "player"   : flags |= RenderFlags.Player;          break;
                            case "event"    : flags |= RenderFlags.Event;           break;
                            case "weather"  : flags |= RenderFlags.Weather;         break;
                            case "loc"      : flags |= RenderFlags.Location;        break;
                        }
                    }
                }

                foreach ( GameLocation loc in Game1.locations )
                {
                    renderQueue.Enqueue(new RenderQueueEntry(loc, flags));
                }
            }
            else if (args[0] == "list")
            {
                if ( Game1.locations.Count == 0 )
                {
                    Log.info("No maps loaded.");
                    return;
                }

                string maps = Game1.locations[0].name;
                foreach (GameLocation loc in Game1.locations)
                {
                    if (loc == Game1.locations[0])
                        continue;
                    maps += ", " + loc.name;
                }

                Log.info("Maps: " + maps);
            }
            else if (args[0] == "current")
            {
                RenderFlags flags = RenderFlags.Tiles | RenderFlags.Location;
                if (args.Length == 2 && args[1] == "all")
                {
                    flags = (RenderFlags)0xFFFF;
                }
                else if ( args.Length > 1 )
                {
                    flags = RenderFlags.None;
                    for ( int i = 1; i < args.Length; ++i )
                    {
                        switch ( args[ i ] )
                        {
                            case "tiles"    : flags |= RenderFlags.Tiles;           break;
                            case "light"    : flags |= RenderFlags.Lighting;        break;
                            case "npcs"     : flags |= RenderFlags.Characters;      break;
                            case "player"   : flags |= RenderFlags.Player;          break;
                            case "event"    : flags |= RenderFlags.Event;           break;
                            case "weather"  : flags |= RenderFlags.Weather;         break;
                            case "loc"      : flags |= RenderFlags.Location;        break;
                        }
                    }
                }

                renderQueue.Enqueue(new RenderQueueEntry(Game1.currentLocation, flags));
            }
            else if ( args[ 0 ] == "help" )
            {
                Log.info("Commands: ");
                Log.info("\texport all [settings] - Export all locations.");
                Log.info("\texport list - Get a list of available maps.");
                Log.info("\texport current [settings] - Export your current location.");
                Log.info("\texport <map_name> [settings] - Export map_name.");
                Log.info("\texport help - Print this block of text.");
                Log.info("Settings: ");
                Log.info("\ttiles - Render the tilemap.");
                Log.info("\tlight - Render lighting.");
                Log.info("\tnpcs - Render NPCs.");
                Log.info("\tevent - Render the current event.");
                Log.info("\tweather - Render weather.");
                Log.info("\tloc - Render things specific to a location.");
                Log.info("\tall - Render all of the above.");
                Log.info("\tThe default is 'tiles buildings furniture features'.");
            }
            else
            {
                GameLocation loc = Game1.getLocationFromName(args[0]);
                if ( loc == null )
                {
                    Log.error("Bad map name");
                    return;
                }

                RenderFlags flags = RenderFlags.Tiles | RenderFlags.Location;
                if ( args.Length == 2 && args[ 1 ] == "all" )
                {
                    flags = (RenderFlags)0xFFFF;
                }
                else if ( args.Length > 1 )
                {
                    flags = RenderFlags.None;
                    for ( int i = 1; i < args.Length; ++i )
                    {
                        switch ( args[ i ] )
                        {
                            case "tiles"    : flags |= RenderFlags.Tiles;           break;
                            case "light"    : flags |= RenderFlags.Lighting;        break;
                            case "npcs"     : flags |= RenderFlags.Characters;      break;
                            case "player"   : flags |= RenderFlags.Player;          break;
                            case "event"    : flags |= RenderFlags.Event;           break;
                            case "weather"  : flags |= RenderFlags.Weather;         break;
                            case "loc"      : flags |= RenderFlags.Location;        break;
                        }
                    }
                }

                renderQueue.Enqueue(new RenderQueueEntry(loc, flags));
            }
        }

        private void checkRenderQueue( object sender, EventArgs args )
        {
            // Simply doing the export when the command is called can cause issues (not
            // actually rendering, making the game skip a frame, crashing, ...) since
            // the console runs on another thread. Rendering might happen at the same 
            // time as the main game. So instead we're going to render one per frame
            // in the update stage, so things definitely don't interfere.

            RenderQueueEntry toRender = null;
            if (!renderQueue.TryDequeue(out toRender))
            {
                return;
            }
            export(toRender);
        }

        private void export(RenderQueueEntry render)
        {
            GameLocation loc = render.loc;

            int oldZoom = Game1.pixelZoom;
            Game1.pixelZoom = 4;
            SpriteBatch b = Game1.spriteBatch;// new SpriteBatch(Game1.graphics.GraphicsDevice);
            GraphicsDevice dev = Game1.graphics.GraphicsDevice;
            var display = Game1.mapDisplayDevice;
            RenderTarget2D output = null;
            Stream stream = null;
            bool begun = false;
            Rectangle oldView = new Rectangle();
            float oldZoomL = Game1.options.zoomLevel;
            Game1.options.zoomLevel = 0.25f;
            try
            {
                Log.info("Rendering " + loc.name + "...");
                output = new RenderTarget2D(dev, loc.map.DisplayWidth, loc.map.DisplayHeight);
                RectangleX viewportX = new RectangleX(0, 0, output.Width, output.Height);
                Rectangle viewport = new Rectangle(0, 0, output.Width, output.Height);
                oldView = Game1.viewport;
                Game1.viewport = viewport;

                if (loc is DecoratableLocation)
                {
                    foreach (Furniture f in (loc as DecoratableLocation).furniture)
                    {
                        f.updateDrawPosition();
                    }
                }

                if ( render.Lighting )
                {
                    if (Game1.drawLighting)
                    {
                        dev.SetRenderTarget(Game1.lightmap);
                        dev.Clear(Color.White * 0f);
                        b.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null);
                        b.Draw(Game1.staminaRect, Game1.lightmap.Bounds, loc.name.Equals("UndergroundMine") ? Game1.mine.getLightingColor(null/*gameTime*/) : ((!Game1.ambientLight.Equals(Color.White) && (!Game1.isRaining || !loc.isOutdoors)) ? Game1.ambientLight : Game1.outdoorLight));
                        for (int i = 0; i < Game1.currentLightSources.Count; i++)
                        {
                            if (Utility.isOnScreen(Game1.currentLightSources.ElementAt(i).position, (int)(Game1.currentLightSources.ElementAt(i).radius * (float)Game1.tileSize * 4f)))
                            {
                                b.Draw(Game1.currentLightSources.ElementAt(i).lightTexture, Game1.GlobalToLocal(viewport, Game1.currentLightSources.ElementAt(i).position) / (float)(Game1.options.lightingQuality / 2), new Microsoft.Xna.Framework.Rectangle?(Game1.currentLightSources.ElementAt(i).lightTexture.Bounds), Game1.currentLightSources.ElementAt(i).color, 0f, new Vector2((float)Game1.currentLightSources.ElementAt(i).lightTexture.Bounds.Center.X, (float)Game1.currentLightSources.ElementAt(i).lightTexture.Bounds.Center.Y), Game1.currentLightSources.ElementAt(i).radius / (float)(Game1.options.lightingQuality / 2), SpriteEffects.None, 0.9f);
                            }
                        }
                        b.End();
                        //dev.SetRenderTarget((Game1.options.zoomLevel == 1f) ? null : this.screen);
                    }
                    if (Game1.bloomDay && Game1.bloom != null)
                    {
                        Game1.bloom.BeginDraw();
                    }
                }
                dev.SetRenderTarget(output);
                dev.Clear(Color.Black);
                {
                    loc.map.LoadTileSheets(display);
                    
                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                    begun = true;
                    if (render.Tiles)
                    {
                        display.BeginScene(b);
                        loc.map.GetLayer("Back").Draw(Game1.mapDisplayDevice, new xTile.Dimensions.Rectangle(0, 0, output.Width, output.Height), xTile.Dimensions.Location.Origin, false, 4);
                    }
                    if ( render.Characters )
                    {
                        var chars = loc.characters;
                        if (render.Event && Game1.CurrentEvent != null && loc == Game1.currentLocation)
                            chars = Game1.CurrentEvent.actors;

                        foreach ( NPC npc in chars )
                        {
                            if ( !npc.swimming && !npc.hideShadow && !npc.isInvisible && !loc.shouldShadowBeDrawnAboveBuildingsLayer( npc.getTileLocation() ) )
                            {
                                b.Draw(Game1.shadowTexture, Game1.GlobalToLocal(viewport, npc.position + new Vector2((float)(npc.sprite.spriteWidth * Game1.pixelZoom) / 2f, (float)(npc.GetBoundingBox().Height + (npc.IsMonster ? 0 : (Game1.pixelZoom * 3))))), new Microsoft.Xna.Framework.Rectangle?(Game1.shadowTexture.Bounds), Color.White, 0f, new Vector2((float)Game1.shadowTexture.Bounds.Center.X, (float)Game1.shadowTexture.Bounds.Center.Y), ((float)Game1.pixelZoom + (float)npc.yJumpOffset / 40f) * npc.scale, SpriteEffects.None, Math.Max(0f, (float)npc.getStandingY() / 10000f) - 1E-06f);
                            }
                        }
                    }
                    if ( render.Player && Game1.currentLocation == loc )
                    {
                        if (Game1.displayFarmer && !Game1.player.swimming && !Game1.player.isRidingHorse() && !Game1.currentLocation.shouldShadowBeDrawnAboveBuildingsLayer(Game1.player.getTileLocation()))
                        {
                            b.Draw(Game1.shadowTexture, Game1.GlobalToLocal(Game1.player.position + new Vector2(32f, 24f)), new Microsoft.Xna.Framework.Rectangle?(Game1.shadowTexture.Bounds), Color.White, 0f, new Vector2((float)Game1.shadowTexture.Bounds.Center.X, (float)Game1.shadowTexture.Bounds.Center.Y), 4f - (((Game1.player.running || Game1.player.usingTool) && Game1.player.FarmerSprite.indexInCurrentAnimation > 1) ? ((float)Math.Abs(FarmerRenderer.featureYOffsetPerFrame[Game1.player.FarmerSprite.CurrentFrame]) * 0.5f) : 0f), SpriteEffects.None, 0f);
                        }
                    }
                    if ( render.Tiles )
                    {
                        loc.map.GetLayer("Buildings").Draw(Game1.mapDisplayDevice, new xTile.Dimensions.Rectangle(0, 0, output.Width, output.Height), xTile.Dimensions.Location.Origin, false, 4);
                        display.EndScene();
                    }
                    b.End();
                    begun = false;

                    b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                    begun = true;
                    if (render.Characters)
                    {
                        var chars = loc.characters;
                        if (render.Event && Game1.CurrentEvent != null && loc == Game1.currentLocation)
                            chars = Game1.CurrentEvent.actors;

                        foreach (NPC npc in chars)
                        {
                            if (!npc.swimming && !npc.hideShadow && !npc.isInvisible && !loc.shouldShadowBeDrawnAboveBuildingsLayer(npc.getTileLocation()))
                            {
                                b.Draw(Game1.shadowTexture, Game1.GlobalToLocal(viewport, npc.position + new Vector2((float)(npc.sprite.spriteWidth * Game1.pixelZoom) / 2f, (float)(npc.GetBoundingBox().Height + (npc.IsMonster ? 0 : (Game1.pixelZoom * 3))))), new Microsoft.Xna.Framework.Rectangle?(Game1.shadowTexture.Bounds), Color.White, 0f, new Vector2((float)Game1.shadowTexture.Bounds.Center.X, (float)Game1.shadowTexture.Bounds.Center.Y), ((float)Game1.pixelZoom + (float)npc.yJumpOffset / 40f) * npc.scale, SpriteEffects.None, Math.Max(0f, (float)npc.getStandingY() / 10000f) - 1E-06f);
                            }
                        }
                    }
                    if ( render.Player && Game1.currentLocation == loc)
                    {
                        if (Game1.displayFarmer && !Game1.player.swimming && !Game1.player.isRidingHorse() && Game1.currentLocation.shouldShadowBeDrawnAboveBuildingsLayer(Game1.player.getTileLocation()))
                        {
                            b.Draw(Game1.shadowTexture, Game1.GlobalToLocal(Game1.player.position + new Vector2(32f, 24f)), new Microsoft.Xna.Framework.Rectangle?(Game1.shadowTexture.Bounds), Color.White, 0f, new Vector2((float)Game1.shadowTexture.Bounds.Center.X, (float)Game1.shadowTexture.Bounds.Center.Y), 4f - (((Game1.player.running || Game1.player.usingTool) && Game1.player.FarmerSprite.indexInCurrentAnimation > 1) ? ((float)Math.Abs(FarmerRenderer.featureYOffsetPerFrame[Game1.player.FarmerSprite.CurrentFrame]) * 0.5f) : 0f), SpriteEffects.None, Math.Max(0.0001f, (float)Game1.player.getStandingY() / 10000f + 0.00011f) - 0.0001f);
                        }
                        if (Game1.displayFarmer)
                        {
                            Game1.player.draw(b);
                        }
                    }
                    if ( render.Event && loc == Game1.currentLocation)
                    {
                        if ((Game1.eventUp || Game1.killScreen) && !Game1.killScreen && Game1.currentLocation.currentEvent != null)
                        {
                            loc.currentEvent.draw(b);
                        }
                    }
                    if ( render.Location )
                    {
                        if (Game1.player.currentUpgrade != null && Game1.player.currentUpgrade.daysLeftTillUpgradeDone <= 3 && loc.Name.Equals("Farm"))
                        {
                            b.Draw(Game1.player.currentUpgrade.workerTexture, Game1.GlobalToLocal(viewport, Game1.player.currentUpgrade.positionOfCarpenter), new Microsoft.Xna.Framework.Rectangle?(Game1.player.currentUpgrade.getSourceRectangle()), Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, (Game1.player.currentUpgrade.positionOfCarpenter.Y + (float)(Game1.tileSize * 3 / 4)) / 10000f);
                        }

                        loc.draw(b);
                    }
                    if ( render.Player && Game1.currentLocation == loc)
                    {
                        if (Game1.player.ActiveObject == null && (Game1.player.UsingTool || Game1.pickingTool) && Game1.player.CurrentTool != null && (!Game1.player.CurrentTool.Name.Equals("Seeds") || Game1.pickingTool))
                        {
                            Game1.drawTool(Game1.player);
                        }
                    }
                    if ( render.Location )
                    {
                        if (loc.Name.Equals("Farm"))
                        {
                            Helper.Reflection.GetPrivateMethod(Game1.game1, "drawFarmBuildings").Invoke();
                        }
                    }
                    if (render.Location)
                    {
                        if (Game1.tvStation >= 0)
                        {
                            b.Draw(Game1.tvStationTexture, Game1.GlobalToLocal(viewport, new Vector2((float)(6 * Game1.tileSize + Game1.tileSize / 4), (float)(2 * Game1.tileSize + Game1.tileSize / 2))), new Microsoft.Xna.Framework.Rectangle?(new Microsoft.Xna.Framework.Rectangle(Game1.tvStation * 24, 0, 24, 15)), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1E-08f);
                        }
                    }
                    if (render.Tiles)
                    {
                        display.BeginScene(b);
                        loc.map.GetLayer("Front").Draw(Game1.mapDisplayDevice, new xTile.Dimensions.Rectangle(0, 0, output.Width, output.Height), xTile.Dimensions.Location.Origin, false, 4);
                        display.EndScene();
                    }
                    if ( render.Location )
                    {
                        loc.drawAboveFrontLayer(b);
                    }
                    b.End();
                    begun = false;

                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                    begun = true;
                    if (render.Location)
                    {
                        if (loc.Name.Equals("Farm") && Game1.stats.SeedsSown >= 200u)
                        {
                            b.Draw(Game1.debrisSpriteSheet, Game1.GlobalToLocal(viewport, new Vector2((float)(3 * Game1.tileSize + Game1.tileSize / 4), (float)(Game1.tileSize + Game1.tileSize / 3))), new Microsoft.Xna.Framework.Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 16, -1, -1)), Color.White);
                            b.Draw(Game1.debrisSpriteSheet, Game1.GlobalToLocal(viewport, new Vector2((float)(4 * Game1.tileSize + Game1.tileSize), (float)(2 * Game1.tileSize + Game1.tileSize))), new Microsoft.Xna.Framework.Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 16, -1, -1)), Color.White);
                            b.Draw(Game1.debrisSpriteSheet, Game1.GlobalToLocal(viewport, new Vector2((float)(5 * Game1.tileSize), (float)(2 * Game1.tileSize))), new Microsoft.Xna.Framework.Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 16, -1, -1)), Color.White);
                            b.Draw(Game1.debrisSpriteSheet, Game1.GlobalToLocal(viewport, new Vector2((float)(3 * Game1.tileSize + Game1.tileSize / 2), (float)(3 * Game1.tileSize))), new Microsoft.Xna.Framework.Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 16, -1, -1)), Color.White);
                            b.Draw(Game1.debrisSpriteSheet, Game1.GlobalToLocal(viewport, new Vector2((float)(5 * Game1.tileSize - Game1.tileSize / 4), (float)Game1.tileSize)), new Microsoft.Xna.Framework.Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 16, -1, -1)), Color.White);
                            b.Draw(Game1.debrisSpriteSheet, Game1.GlobalToLocal(viewport, new Vector2((float)(4 * Game1.tileSize), (float)(3 * Game1.tileSize + Game1.tileSize / 6))), new Microsoft.Xna.Framework.Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 16, -1, -1)), Color.White);
                            b.Draw(Game1.debrisSpriteSheet, Game1.GlobalToLocal(viewport, new Vector2((float)(4 * Game1.tileSize + Game1.tileSize / 5), (float)(2 * Game1.tileSize + Game1.tileSize / 3))), new Microsoft.Xna.Framework.Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 16, -1, -1)), Color.White);
                        }
                    }
                    if ( render.Player && Game1.currentLocation == loc)
                    {
                        var meth = typeof(Game1).GetMethod("checkBigCraftableBoundariesForFrontLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance );
                        if (Game1.displayFarmer && Game1.player.ActiveObject != null && Game1.player.ActiveObject.bigCraftable && (bool) meth.Invoke( Game1.game1, new object[] { } ) && Game1.currentLocation.Map.GetLayer("Front").PickTile(new Location(Game1.player.getStandingX(), Game1.player.getStandingY()), viewport.Size) == null)
                        {
                            Game1.drawPlayerHeldObject(Game1.player);
                        }
                        else if (Game1.displayFarmer && Game1.player.ActiveObject != null && ((Game1.currentLocation.Map.GetLayer("Front").PickTile(new Location((int)Game1.player.position.X, (int)Game1.player.position.Y - Game1.tileSize * 3 / 5), viewport.Size) != null && !Game1.currentLocation.Map.GetLayer("Front").PickTile(new Location((int)Game1.player.position.X, (int)Game1.player.position.Y - Game1.tileSize * 3 / 5), viewport.Size).TileIndexProperties.ContainsKey("FrontAlways")) || (Game1.currentLocation.Map.GetLayer("Front").PickTile(new Location(Game1.player.GetBoundingBox().Right, (int)Game1.player.position.Y - Game1.tileSize * 3 / 5), viewport.Size) != null && !Game1.currentLocation.Map.GetLayer("Front").PickTile(new Location(Game1.player.GetBoundingBox().Right, (int)Game1.player.position.Y - Game1.tileSize * 3 / 5), viewport.Size).TileIndexProperties.ContainsKey("FrontAlways"))))
                        {
                            Game1.drawPlayerHeldObject(Game1.player);
                        }
                        if ((Game1.player.UsingTool || Game1.pickingTool) && Game1.player.CurrentTool != null && (!Game1.player.CurrentTool.Name.Equals("Seeds") || Game1.pickingTool) && Game1.currentLocation.Map.GetLayer("Front").PickTile(new Location(Game1.player.getStandingX(), (int)Game1.player.position.Y - Game1.tileSize * 3 / 5), viewport.Size) != null && Game1.currentLocation.Map.GetLayer("Front").PickTile(new Location(Game1.player.getStandingX(), Game1.player.getStandingY()), viewport.Size) == null)
                        {
                            Game1.drawTool(Game1.player);
                        }
                    }
                    if (render.Tiles)
                    {
                        if (loc.map.GetLayer("AlwaysFront") != null)
                        {
                            display.BeginScene(b);
                            loc.map.GetLayer("AlwaysFront").Draw(Game1.mapDisplayDevice, new xTile.Dimensions.Rectangle(0, 0, output.Width, output.Height), xTile.Dimensions.Location.Origin, false, 4);
                            display.EndScene();
                        }
                    }
                    if ( render.Player && Game1.currentLocation == loc)
                    {
                        if (Game1.toolHold > 400f && Game1.player.CurrentTool.UpgradeLevel >= 1 && Game1.player.canReleaseTool)
                        {
                            Color color = Color.White;
                            switch ((int)(Game1.toolHold / 600f) + 2)
                            {
                                case 1:
                                    color = Tool.copperColor;
                                    break;
                                case 2:
                                    color = Tool.steelColor;
                                    break;
                                case 3:
                                    color = Tool.goldColor;
                                    break;
                                case 4:
                                    color = Tool.iridiumColor;
                                    break;
                            }
                            b.Draw(Game1.littleEffect, new Microsoft.Xna.Framework.Rectangle((int)Game1.player.getLocalPosition(viewport).X - 2, (int)Game1.player.getLocalPosition(viewport).Y - (Game1.player.CurrentTool.Name.Equals("Watering Can") ? 0 : Game1.tileSize) - 2, (int)(Game1.toolHold % 600f * 0.08f) + 4, Game1.tileSize / 8 + 4), Color.Black);
                            b.Draw(Game1.littleEffect, new Microsoft.Xna.Framework.Rectangle((int)Game1.player.getLocalPosition(viewport).X, (int)Game1.player.getLocalPosition(viewport).Y - (Game1.player.CurrentTool.Name.Equals("Watering Can") ? 0 : Game1.tileSize), (int)(Game1.toolHold % 600f * 0.08f), Game1.tileSize / 8), color);
                        }
                    }
                    if ( render.Weather )
                    {
                        if (Game1.isDebrisWeather && loc.IsOutdoors && !loc.ignoreDebrisWeather && !loc.Name.Equals("Desert") && viewport.X > -10)
                        {
                            using (List<WeatherDebris>.Enumerator enumerator4 = Game1.debrisWeather.GetEnumerator())
                            {
                                while (enumerator4.MoveNext())
                                {
                                    enumerator4.Current.draw(b);
                                }
                            }
                        }
                    }
                    if ( render.Event )
                    {
                        if (Game1.farmEvent != null)
                        {
                            Game1.farmEvent.draw(b);
                        }
                    }
                    if ( render.Lighting )
                    {
                        if (loc.LightLevel > 0f && Game1.timeOfDay < 2000)
                        {
                            b.Draw(Game1.fadeToBlackRect, viewportX, Color.Black * loc.LightLevel);
                        }
                        if (Game1.screenGlow)
                        {
                            b.Draw(Game1.fadeToBlackRect, viewportX, Game1.screenGlowColor * Game1.screenGlowAlpha);
                        }
                    }
                    if ( render.Location )
                    {
                        loc.drawAboveAlwaysFrontLayer(b);
                    }
                    if ( render.Player && Game1.currentLocation == loc)
                    {
                        if (Game1.player.CurrentTool != null && Game1.player.CurrentTool is FishingRod && ((Game1.player.CurrentTool as FishingRod).isTimingCast || (Game1.player.CurrentTool as FishingRod).castingChosenCountdown > 0f || (Game1.player.CurrentTool as FishingRod).fishCaught || (Game1.player.CurrentTool as FishingRod).showingTreasure))
                        {
                            Game1.player.CurrentTool.draw(b);
                        }
                    }
                    if ( render.Weather )
                    {
                        if (Game1.isRaining && loc.IsOutdoors && !loc.Name.Equals("Desert") && !(loc is Summit) && (!Game1.eventUp || loc.isTileOnMap(new Vector2((float)(viewport.X / Game1.tileSize), (float)(viewport.Y / Game1.tileSize)))))
                        {
                            for (int j = 0; j < Game1.rainDrops.Length; j++)
                            {
                                b.Draw(Game1.rainTexture, Game1.rainDrops[j].position, new Microsoft.Xna.Framework.Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.rainTexture, Game1.rainDrops[j].frame, -1, -1)), Color.White);
                            }
                        }
                    }
                    b.End();
                    begun = false;

                    if ( render.Event && Game1.currentLocation == loc)
                    {
                        b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                        begun = true;
                        if (Game1.eventUp && Game1.currentLocation.currentEvent != null)
                        {
                            foreach (NPC current7 in Game1.currentLocation.currentEvent.actors)
                            {
                                if (current7.isEmoting)
                                {
                                    Vector2 localPosition = current7.getLocalPosition(viewport);
                                    localPosition.Y -= (float)(Game1.tileSize * 2 + Game1.pixelZoom * 3);
                                    if (current7.age == 2)
                                    {
                                        localPosition.Y += (float)(Game1.tileSize / 2);
                                    }
                                    else if (current7.gender == 1)
                                    {
                                        localPosition.Y += (float)(Game1.tileSize / 6);
                                    }
                                    b.Draw(Game1.emoteSpriteSheet, localPosition, new Microsoft.Xna.Framework.Rectangle?(new Microsoft.Xna.Framework.Rectangle(current7.CurrentEmoteIndex * (Game1.tileSize / 4) % Game1.emoteSpriteSheet.Width, current7.CurrentEmoteIndex * (Game1.tileSize / 4) / Game1.emoteSpriteSheet.Width * (Game1.tileSize / 4), Game1.tileSize / 4, Game1.tileSize / 4)), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, (float)current7.getStandingY() / 10000f);
                                }
                            }
                        }
                        b.End();
                        begun = false;
                    }

                    if ( render.Lighting )
                    {
                        if (Game1.drawLighting)
                        {
                            b.Begin(SpriteSortMode.Deferred, Helper.Reflection.GetPrivateField< BlendState >( Game1.game1, "lightingBlend" ).GetValue(), SamplerState.LinearClamp, null, null);
                            begun = true;
                            b.Draw(Game1.lightmap, Vector2.Zero, new Microsoft.Xna.Framework.Rectangle?(Game1.lightmap.Bounds), Color.White, 0f, Vector2.Zero, (float)(Game1.options.lightingQuality / 2), SpriteEffects.None, 1f);
                            if (Game1.isRaining && loc.isOutdoors && !(loc is Desert))
                            {
                                b.Draw(Game1.staminaRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.OrangeRed * 0.45f);
                            }
                            b.End();
                            begun = false;
                        }
                    }
                }
                dev.SetRenderTarget(null);

                string name = loc.name;
                if ( loc.uniqueName != null )
                    name = loc.uniqueName;

                string dirPath = Helper.DirectoryPath + "/../../MapExport";
                string imagePath = dirPath + "/" + name + ".png";
                Log.info("Saving " + name + " to " + imagePath + "...");

                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                stream = File.Create(imagePath);
                output.SaveAsPng(stream, output.Width, output.Height);
            }
            catch (Exception e)
            {
                Log.error("Exception: " + e);
            }
            finally
            {
                display.EndScene();
                if ( begun )
                    b.End();
                dev.SetRenderTarget(null);
                if ( stream != null )
                    stream.Dispose();
                if ( output != null )
                    output.Dispose();
                Game1.pixelZoom = oldZoom;
                Game1.viewport = oldView;
                Game1.options.zoomLevel = oldZoomL;
            }

            if (loc is DecoratableLocation)
            {
                foreach (Furniture f in (loc as DecoratableLocation).furniture)
                {
                    f.updateDrawPosition();
                }
            }
        }
    }
}
