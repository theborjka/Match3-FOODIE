# Match3 Foodie

Component-based starter for a classic Match-3 board.

## Quick start

1. Open Unity.
2. Run `Tools > Match3 Foodie > Create Starter Setup`.
3. Press Play.
4. Swap pieces by dragging one piece or clicking two adjacent pieces.

## Main assets

- `Match3Board` controls board generation, input, swapping, matching, clearing, falling, and refill.
- `Match3BoardSettings` configures width, height, cell size, match patterns, timings, piece prefab, and available elements.
- `Match3ElementDefinition` configures each element sprite, tint, spawn weight, optional custom piece prefab, and destruction effect prefab.
- `Match3PieceView` owns the visual state, movement tween, selection scale, and destroy effect spawn.
- `Match3LevelSettings` configures timer duration and shopping-list goals.
- `Match3LevelController` tracks remaining time, collected goal progress, win, and fail events.
- `Match3LevelHud` binds level state to your own UI: assign a TMP timer text, a goals root, and a `Match3GoalItemView` prefab or pre-placed goal views.
- `Match3GoalItemView` binds one shopping-list item to your own UI Image and TMP amount text.
- `Match3BoosterController` binds three gameplay boosters to your own UI buttons and TMP uses-left labels: pop one piece, pop all pieces of a selected color, or random-walk across connected pieces and clear the path.

Generated starter assets are placed in `Assets/Match3Foodie/Generated`.
