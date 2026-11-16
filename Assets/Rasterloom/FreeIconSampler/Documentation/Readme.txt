RASTERLOOM - 60 FREE GAME UI ICONS
Version 1.0.2 - free, royalty-free, commercial use allowed

WHAT IS IN HERE
  Textures/White/<Category>/T_<Name>.png   60 white sprites, 256 px
  Textures/Black/<Category>/T_<Name>.png   the same 60 in black
  Scenes/IconSampler.unity                 all 60, camera tours them
  Scripts/IconTourCamera.cs                the camera move in that scene
  Scripts/IconTint.cs                      the tint cycle - read the comment
  Documentation/                           contents, licence

These sixty are a cross-section of the nine categories in the full Rasterloom
Game UI icon set: interface, combat, items, crafting, stats, world, social,
input and economy. They are the same files at the same quality under the same
licence as the paid pack - nothing is watermarked, nothing is crippled.

WHITE OR BLACK - AND WHY BOTH SHIP
Every icon is a flat shape on transparency. On a dark interface use the white
set and tint it: Image.color in UI, or the SpriteRenderer colour in a 2D
scene. One texture then serves the normal, hover, disabled, gold and danger
states, with no extra memory and no extra draw call. Press Play in the demo
scene to watch that happen.

On a light interface, tinting white down to near-black leaves you fighting
the anti-aliased alpha at the edges, which reads as a grey fringe. That is
what the black set is for.

IMPORT SETTINGS ALREADY APPLIED
  Texture Type       Sprite (2D and UI)
  Filter Mode        Bilinear
  Compression        None
  Generate Mip Maps  off
  Wrap Mode          Clamp
  Mesh Type          Full Rect
  Pixels Per Unit    256

Mip maps off and compression none are the two that matter. A mipped UI icon
goes soft the moment the canvas scales below 1:1, and block compression puts
colour artefacts along the one-pixel edge of a flat shape, where there is
nothing to hide them.

THE FULL SET - 500 GAME UI ICONS
These sixty come from a set of five hundred and twenty: the same nine
categories, the same grid and stroke, white and black, with a gallery scene
that tours all of them. Because they share one optical weight,
icons from the full set sit next to the ones you already placed without
looking like a second artist drew them.

  500 Game UI Icons - SVG, PNG & Sprite Sheet
  https://assetstore.unity.com/packages/2d/gui/icons/500-game-ui-icons-svg-png-sprite-sheet-405680

The demo scene has a button that opens the same page.

HOW THEY WERE MADE
Every icon is geometry on a 24 x 24 grid with a 2 px stroke, drawn by a
program written for this set. Nothing is traced and nothing comes from an
image generator. That is why five hundred of them share one optical weight.

IF THEY HELPED
This pack is free, and the only thing that decides whether anyone else finds
it is a rating on its store page. If the icons were useful, a line or two
there is the most useful thing you can send back:

  https://assetstore.unity.com/packages/2d/gui/icons/60-free-game-ui-icons-svg-png-tintable-sprites-407082#reviews

Missing an icon you needed? Say which one in the review or through the
publisher contact on the same page - requests decide what gets drawn next.

VERSION HISTORY
  1.0.2  Store links in this file and a two-button overlay in the demo
         scene. Icons unchanged.
  1.0.1  Re-uploaded from Unity 2022.3 LTS. No content changes.
  1.0    First release.

LICENCE
Royalty-free, unlimited commercial and personal projects, no attribution
required. You may not resell or redistribute the icon files themselves as an
asset pack. Full terms in Documentation/LICENSE.txt.
