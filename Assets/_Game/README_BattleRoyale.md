# Battle Royale Mobile Prototype

Open `Assets/_Game/Scenes/BattleRoyalePrototype.unity` and press Play.

Controls in the Editor:

- `WASD`: move
- Mouse drag while holding a mouse button: camera
- Mouse movement after clicking Play/Game view: camera
- Left mouse: fire
- Right mouse: aim
- `Space`: jump or drop from aircraft
- `E`: collect nearest loot
- `R`: reload; on result screen, restart
- `1` / `2`: swap weapon
- `Q`: swap camera shoulder
- `4`: use med kit
- `Esc`: pause or resume

The user-supplied map is imported at `Resources/UserMap/clock_tower_free_fire_model.glb`. Its embedded metadata declares CC BY 4.0; attribution is stored beside the asset and must be reverified before commercial release.

In the Unity Editor, mobile touch buttons are hidden so keyboard and mouse testing is not blocked. Device builds provide movement, camera, fire, aim, jump/drop, interact, reload, heal, and weapon-swap touch controls positioned from `Screen.safeArea`.
