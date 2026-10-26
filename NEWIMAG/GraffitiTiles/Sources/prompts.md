# image_gen prompts

Built-in image_gen; opaque outputs. References inspected: StyleRef/ref_logo.png and TileConceptV2/A/_1x/Tile_Play_{Floor,Wall}.png.

## Floor attempt 1

Use case: stylized-concept. Production Re:Trap pixel art FLOOR Wang tile sheet. References visible above: crimson dry spray/drips from Re:Trap logo; 32px concrete tile dark cool gray chipped concrete with a thin level ledge on top.
Generate ONE square 768x768 image representing exactly 96x96 logical pixels enlarged precisely 8x nearest neighbor. EXACT 3x3 touching cells, each 32x32 logical pixels, NO gutters NO labels NO outside margin. Opaque.
Rows are LEFT edge type 0,1,2; columns RIGHT edge type 0,1,2. 0=bare concrete no paint at edge. 1=thick crimson ribbon enters/exits horizontally at logical rows 16-19 (4px). 2=thin ivory spray stroke enters/exits at rows 22-23 (2px). Every matching edge identical height. In first/last 4 columns keep incoming paint horizontal and at these heights. Each cell thin identical gray walking ledge at rows 0-3, dark cracked stained gray face below.
Nine DISTINCT continuous gestures: [0,0] small isolated slash with splatter; [0,1] tapered start growing into red rising zigzag to right; [0,2] tapered ivory hooked stroke to right; [1,0] red curve from left ending in drips before right; [1,1] uninterrupted crimson angular crown/teeth waveform connecting both edges, not a floating crown stamp; [1,2] crimson flowing stroke crosses into ivory angular ribbon joining right lower port; [2,0] ivory looping stroke from left tapering into splatters; [2,1] ivory descending curve transforms into red rising ribbon joining right; [2,2] flowing ivory zigzag with a crossing small crimson slash. Thick red sprays, narrow ivory highlights, organic dry splatter and drips. Paths must be continuous from ports through interiors. No separate icon stamps or bordered squares. Keep concrete texture muted and paint bold. Palette near #303940 #3B454C #47525A #55616A #65717A #75818A #19181F #641F36 #92223F #B83450 #DC526B #E7D8CE #F7EEE4. Strict large pixel clusters, no smooth vector lines, no antialiasing.

## Floor attempt 2

Use case: stylized-concept. Production Re:Trap pixel art FLOOR Wang tile sheet. References visible above: crimson dry spray/drips from Re:Trap logo; 32px concrete tile dark cool gray chipped concrete with a thin level ledge on top.
Generate ONE square 768x768 image representing exactly 96x96 logical pixels enlarged precisely 8x nearest neighbor. EXACT 3x3 touching cells, each 32x32 logical pixels, NO gutters NO labels NO outside margin. Opaque.
Rows are LEFT edge type 0,1,2; columns RIGHT edge type 0,1,2. 0=bare concrete no paint at edge. 1=thick crimson ribbon enters/exits horizontally at logical rows 16-19 (4px). 2=thin ivory spray stroke enters/exits at rows 22-23 (2px). Every matching edge identical height. In first/last 4 columns keep incoming paint horizontal and at these heights. Each cell thin identical gray walking ledge at rows 0-3, dark cracked stained gray face below.
Nine DISTINCT continuous gestures: [0,0] small isolated slash with splatter; [0,1] tapered start growing into red rising zigzag to right; [0,2] tapered ivory hooked stroke to right; [1,0] red curve from left ending in drips before right; [1,1] uninterrupted crimson angular crown/teeth waveform connecting both edges, not a floating crown stamp; [1,2] crimson flowing stroke crosses into ivory angular ribbon joining right lower port; [2,0] ivory looping stroke from left tapering into splatters; [2,1] ivory descending curve transforms into red rising ribbon joining right; [2,2] flowing ivory zigzag with a crossing small crimson slash. Thick red sprays, narrow ivory highlights, organic dry splatter and drips. Paths must be continuous from ports through interiors. No separate icon stamps or bordered squares. Keep concrete texture muted and paint bold. Palette near #303940 #3B454C #47525A #55616A #65717A #75818A #19181F #641F36 #92223F #B83450 #DC526B #E7D8CE #F7EEE4. Strict large pixel clusters, no smooth vector lines, no antialiasing.
CRITICAL CORRECTION: all crimson edge ports MUST sit at the SAME y=18 center on BOTH LEFT AND RIGHT, not near upper or lower corners. All white ports at SAME y=23 center. 0 port MUST have no paint in first/last 6 pixels. The tile at row0 col1 only touches RIGHT edge (never left); row0 col2 only touches RIGHT; row1 col0 only LEFT; row2 col0 only LEFT. Keep first and last 5 columns as straight level connector necks. EXACTLY 32x32 coarse pixels per tile: do not render tiny texture finer than these. Use 1024x1024 output with a 4x4 grid of 32x32 tiles at 8x; put the nine tiles in the top-left 3x3 cells (768x768), final column and bottom row SOLID dark #19181F unused padding. No grout lines.

## Floor attempt 3 — selected

Generate a coarse pixel art game texture atlas, 768x768, 96x96 logical pixels enlarged 8x. Nine square 32x32 tiles in 3x3 grid filling image, no margins, no lines separating cells. Dark blue gray cracked concrete #3B454C #47525A #55616A, narrow straight concrete top ledge. Crimson and ivory sprayed paint with drips. THIS IS A WANG CONNECTOR ATLAS, not nine separate pictures. Every red port centered at 55% tile height, every ivory port at 72% height. Red port 4 logical pixels thick, ivory port 2px thick. Paint stays level near edges.
Top row: left edge of ALL THREE tiles completely bare gray. Top-left isolated tiny red slash. Top-middle starts in center as narrow spray tip then runs horizontally RIGHT to right edge at 55% height. Top-right white curved stroke starts in center and exits RIGHT at 72% height.
Middle row: ALL THREE enter LEFT as thick red paint at 55% height. Middle-left red wave terminates before right edge in tapered drips, rightmost 20% bare gray. Middle-middle red teeth-shaped zigzag connects LEFT and RIGHT at exactly same 55% height. Middle-right red curve enters LEFT 55%, transforms into ivory, exits RIGHT at 72%.
Bottom row: ALL THREE enter LEFT as thin ivory at 72%. Bottom-left ivory hook terminates at center, rightmost 20% bare gray. Bottom-middle ivory wave enters LEFT at 72%, turns red and exits RIGHT at 55%. Bottom-right ivory irregular zigzag connects both edges at exactly 72%, crossed by one interior red slash.
DO NOT introduce different port heights. These are horizontal paths through tiles. Organic paint, NO floating crown stamps. Identical level walking ledge in top 3 logical rows of every cell. Genuine chunky 32x32 pixel artwork per cell, not high res illustration.

## Wall attempt 1

Production pixel art Re:Trap WALL Wang atlas. ONE 768x768 square, exactly 3x3 touching square tiles, each 32x32 logical pixels enlarged 8x. No margins no labels no gutters. Dark blue gray cracked concrete #303940 #3B454C #47525A #55616A #65717A with bold crimson #B83450 #92223F and ivory #F7EEE4 dry sprayed strokes and gravity drips. Coarse pixels.
This is a VERTICAL CONNECTOR atlas. Grid ROW is TOP port 0,1,2; grid COLUMN is BOTTOM port 0,1,2. Type0 completely bare concrete edge, type1 thick red stripe crosses at x=16 center with width4; type2 thin white stroke crosses at x=23 center width2. All red ports same X coordinate at TOP and BOTTOM. All ivory same X coordinate. Keep 4px near top/bottom vertically straight.
Cell top-left: isolated red sprayed slash, no paint touches top/bottom.
Cell top-middle: red tapered spray starts inside middle of tile, widens and flows DOWN to bottom center red port. TOP blank.
Cell top-right: ivory hooked line starts inside tile, flows DOWN into bottom white port at x23. TOP blank.
Cell middle-left: red enters TOP center, curves, ends in slender drips BEFORE bottom, BOTTOM blank.
Cell middle-middle: red enters TOP center, zigzags left-right as teeth then exits BOTTOM center. Unbroken vertical red gesture.
Cell middle-right: red enters TOP x16, curves down and changes to ivory exiting BOTTOM x23.
Cell bottom-left: ivory enters TOP x23, makes hooked flowing gesture and tapers BEFORE bottom, BOTTOM blank.
Cell bottom-middle: ivory enters TOP x23, curves down transforms into red exits BOTTOM x16.
Cell bottom-right: white enters TOP x23, snakes vertically down through two angular turns, exits BOTTOM x23; short red slash crosses interior.
Keep sparse organic paint flecks and thin drips. No horizontal ledge, no horizontal bars dividing cells. Concrete extends to all edges. Not separate floating icons. Do not rotate a floor texture; gravity streaks downward.

## Wall attempt 2 — selected

Production pixel art Re:Trap WALL Wang atlas. ONE 768x768 square, exactly 3x3 touching square tiles, each 32x32 logical pixels enlarged 8x. No margins no labels no gutters. Dark blue gray cracked concrete #303940 #3B454C #47525A #55616A #65717A with bold crimson #B83450 #92223F and ivory #F7EEE4 dry sprayed strokes and gravity drips. Coarse pixels.
This is a VERTICAL CONNECTOR atlas. Grid ROW is TOP port 0,1,2; grid COLUMN is BOTTOM port 0,1,2. Type0 completely bare concrete edge, type1 thick red stripe crosses at x=16 center with width4; type2 thin white stroke crosses at x=23 center width2. All red ports same X coordinate at TOP and BOTTOM. All ivory same X coordinate. Keep 4px near top/bottom vertically straight.
Cell top-left: isolated red sprayed slash, no paint touches top/bottom.
Cell top-middle: red tapered spray starts inside middle of tile, widens and flows DOWN to bottom center red port. TOP blank.
Cell top-right: ivory hooked line starts inside tile, flows DOWN into bottom white port at x23. TOP blank.
Cell middle-left: red enters TOP center, curves, ends in slender drips BEFORE bottom, BOTTOM blank.
Cell middle-middle: red enters TOP center, zigzags left-right as teeth then exits BOTTOM center. Unbroken vertical red gesture.
Cell middle-right: red enters TOP x16, curves down and changes to ivory exiting BOTTOM x23.
Cell bottom-left: ivory enters TOP x23, makes hooked flowing gesture and tapers BEFORE bottom, BOTTOM blank.
Cell bottom-middle: ivory enters TOP x23, curves down transforms into red exits BOTTOM x16.
Cell bottom-right: white enters TOP x23, snakes vertically down through two angular turns, exits BOTTOM x23; short red slash crosses interior.
Keep sparse organic paint flecks and thin drips. No horizontal ledge, no horizontal bars dividing cells. Concrete extends to all edges. Not separate floating icons. Do not rotate a floor texture; gravity streaks downward.
Corrections required: all 3 top-row cells TOP edges must be bare concrete, including top-right. TOP-RIGHT is a short tapered ivory line that starts at y=14 x=23 then runs vertically to BOTTOM x=23, no paint above y=10. All bottom-row TOP white lines must be at x=23, i.e. 72% of tile width, never in center. Bottom-right same white line exits x=23. Make every ivory line thin 2px width. Crisp coarse pixel blocks at 32x32 per cell, less fine texture.
