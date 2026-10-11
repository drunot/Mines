using System;
using Avalonia;
using Avalonia.Media;

namespace Mines;

// The faces on the reset button, from the layers of resources/Faces.Inkscape.svg.
public enum Face
{
    Normal,
    Excited,   // a mouse button is held down on the board
    Won,
    Dead,
}

// Drawing shared by every tile-shaped control (BoardView cells, FaceButton): the tile, its bevel and the icons.
// Everything is in tile units (a 32 x 32 tile); callers scale and position it with transforms.
internal static class TileShapes
{
    // ---- Tile layout, in tile units. Matches the old 32 px Fluent buttons. ----

    public const double TileSize = 32;
    public const double BevelWidth = 4;
    public const double IconInset = 6;      // button border (1) + bevel (4) + icon margin (1)
    public const double IconSize = 20;
    public const double NumberFontSize = 18;

    // Pressed tiles shrink slightly around their centre, like Fluent buttons. Dispose the result to undo it.
    public static DrawingContext.PushedState? PushPressed(DrawingContext context, bool pressed) => pressed
        ? context.PushTransform(Matrix.CreateTranslation(-TileSize / 2, -TileSize / 2) * Matrix.CreateScale(0.98, 0.98)
                                * Matrix.CreateTranslation(TileSize / 2, TileSize / 2))
        : null;

    // The tile itself: background, and the bevel for a raised (hidden) tile.
    public static void DrawBase(DrawingContext context, TilePalette palette, IBrush background, bool raised, bool hover)
    {
        // The button background reaches the middle of its transparent 1 px border, hence the 0.5 inset.
        context.DrawRectangle(background, null, new Rect(0.5, 0.5, TileSize - 1, TileSize - 1),
            palette.CornerRadius, palette.CornerRadius);
        if (raised)
        {
            context.DrawGeometry(hover ? palette.BevelHover : palette.Bevel, null, Bevel);
        }
    }

    // Draws an icon in its SVG's own coordinates, scaled into the tile's 20x20 icon area.
    // The flag, bomb and question mark SVGs are drawn with their translate(...); the face layers are not.
    public static void DrawIcon(DrawingContext context, Action draw, bool apply_svg_offset = true)
    {
        double scale = IconSize / SvgSize;
        Matrix svg = apply_svg_offset ? Matrix.CreateTranslation(SvgOffset) : Matrix.Identity;
        using (context.PushTransform(svg * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(IconInset, IconInset)))
        {
            draw();
        }
    }

    // ---- Faces (resources/Faces.Inkscape.svg): a shared background layer plus one layer per face ----

    public static void DrawFace(DrawingContext context, TilePalette palette, Face face)
    {
        // Background layer; the only layer with the SVG's translate(...).
        using (context.PushTransform(Matrix.CreateTranslation(SvgOffset)))
        {
            context.DrawEllipse(palette.Face, new Pen(palette.FaceLine, 6), new Point(105, 105.73283), 95.605156, 95.605156);
        }

        Pen Line(double width) => new Pen(palette.FaceLine, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        switch (face)
        {
            case Face.Normal:
                context.DrawGeometry(null, Line(6.96309), SmileMouth);
                DrawEyes(context, palette);
                break;

            case Face.Dead:
                context.DrawGeometry(null, Line(6.96309), DeadMouth);
                Pen cross = Line(6);
                context.DrawLine(cross, new Point(59.582046, 82.539123), new Point(78.33777, 63.783402));
                context.DrawLine(cross, new Point(78.337767, 82.539123), new Point(59.582048, 63.783402));
                context.DrawLine(cross, new Point(124.47139, 82.539123), new Point(143.22712, 63.783402));
                context.DrawLine(cross, new Point(143.22712, 82.539123), new Point(124.4714, 63.783402));
                break;

            case Face.Won:
                context.DrawGeometry(null, Line(6), WonMouth);
                Pen arm = Line(5.89858);
                context.DrawLine(arm, new Point(5.7487106, 101.4553), new Point(49.572016, 65.210456));
                context.DrawLine(arm, new Point(197.06045, 101.4553), new Point(153.23714, 65.210458));
                using (context.PushTransform(Matrix.CreateTranslation(0.58321186, 0.16914441)))
                {
                    foreach (double x in new[] { 2.6317214, 59.992288 })
                    {
                        using (context.PushTransform(new Matrix(0.94819542, 0, 0, 1.0323839, x, 4.4036739)))
                        {
                            context.DrawGeometry(palette.FaceLine, Line(6.00001), SunglassLens);
                        }
                    }
                }
                context.DrawLine(Line(6.00001), new Point(77.940514, 65.261171), new Point(124.86864, 65.261171));
                break;

            case Face.Excited:
                DrawEyes(context, palette);
                context.DrawEllipse(null, Line(6.00001), new Point(101.40458, 140.9771), 24.32061, 24.32061);
                break;
        }
    }

    private static void DrawEyes(DrawingContext context, TilePalette palette)
    {
        using (context.PushTransform(Matrix.CreateTranslation(-4.492836, 0)))
        {
            context.DrawEllipse(palette.FaceLine, null, new Point(73.452744, 73.161263), 13.699519, 13.699519);
            context.DrawEllipse(palette.FaceLine, null, new Point(138.34209, 73.161263), 13.699519, 13.699519);
        }
    }

    private static Geometry SmileMouth => _smile_mouth ??= Geometry.Parse(
        "m 48.253698,119.79793 c 9.618817,20.60042 23.796347,36.24482 53.150622,36.24482 h 0.00053 c 29.35426,0 43.53179,-15.6444 53.15061,-36.24482");

    private static Geometry DeadMouth => _dead_mouth ??= Geometry.Parse(
        "m 154.55546,158.05133 c -9.61882,-20.60042 -23.79635,-36.24482 -53.15062,-36.24482 h -0.00053 c -29.354261,0 -43.531791,15.6444 -53.150611,36.24482");

    private static Geometry WonMouth => _won_mouth ??= Geometry.Parse(
        "m 55.084388,128.10705 c 8.382657,14.79982 20.738161,26.03912 46.319962,26.03912 h 0.00047 c 25.58178,0 37.93729,-11.2393 46.31995,-26.03912");

    private static Geometry SunglassLens => _sunglass_lens ??= Geometry.Parse(
        "m 50.134411,58.87878 h 46.34518 v 8.190564 A 22.414745,22.414745 135 0 1 74.064846,89.484089 l -1.51569,0 A 22.414745,22.414745 45 0 1 50.134411,67.069344 Z");

    private static Geometry? _smile_mouth;
    private static Geometry? _dead_mouth;
    private static Geometry? _won_mouth;
    private static Geometry? _sunglass_lens;

    // ---- Bevel and the cell icons (resources/Flag.svg, Bomb.svg, Question.svg) ----

    public const double SvgSize = 202.80916;
    public static readonly Vector SvgOffset = new(-3.5954208, -4.3282442);   // the SVGs' translate(...)

    // 4 px ring inside the button's 1 px border. The old Border had CornerRadius 1, which Avalonia applies to
    // the middle of the 4 px stroke, so the outer corners have a radius of 1 + 4 / 2 = 3.
    public static Geometry Bevel => _bevel ??= CreateBevel();

    private static Geometry CreateBevel()
    {
        var outer = new RectangleGeometry(new Rect(1, 1, TileSize - 2, TileSize - 2), 1 + BevelWidth / 2, 1 + BevelWidth / 2);
        var inner = new RectangleGeometry(new Rect(1 + BevelWidth, 1 + BevelWidth,
            TileSize - 2 - 2 * BevelWidth, TileSize - 2 - 2 * BevelWidth));
        return new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
    }

    public static void DrawFlag(DrawingContext context, TilePalette palette)
    {
        context.DrawRectangle(palette.FlagPole, null, new Rect(99.332062, 17.519085, 11.335877, 147.98474));
        context.DrawGeometry(palette.FlagCloth, null, FlagCloth);
        context.DrawRectangle(palette.FlagPole, null, new Rect(50.587791, 149.01527, 108.82442, 43.694656), 12.572519, 12.572519);
        context.DrawRectangle(palette.FlagPole, null, new Rect(65.42749, 128.60954, 79.145027, 35.038166), 10.081738, 10.081738);
    }

    public static void DrawBomb(DrawingContext context, TilePalette palette)
    {
        context.DrawGeometry(palette.BombIcon, null, Bomb);
    }

    public static void DrawCog(DrawingContext context, TilePalette palette)
    {
        context.DrawGeometry(palette.CogIcon, null, Cog);
    }

    public static void DrawQuestion(DrawingContext context, TilePalette palette)
    {
        context.DrawGeometry(palette.QuestionIcon, null, Question);
        context.DrawEllipse(palette.QuestionIcon, null, new Point(108.14073, 167.42484), 19.237623, 19.237623);
    }

    private static Geometry FlagCloth => _flag_cloth ??= Geometry.Parse(
        "M 99.332062,25.352991 V 61.58277 c 3.306338,27.065855 23.666828,18.343242 55.075738,23.60826 34.41846,5.769501 50.90822,21.16002 50.90822,21.16002 C 158.35962,50.454505 169.23346,24.46939 99.332062,25.352991 Z");

    // "F1": fill with the nonzero rule, like SVG does by default.
    private static Geometry Bomb => _bomb ??= Geometry.Parse(
        "F1 m 99.022671,6.5952893 c -7.193541,0 -12.984737,5.7911967 -12.984737,12.9847367 V 33.784844 A 74.404572,74.404572 0 0 0 67.53312,41.450009 L 57.488772,31.405661 c -5.086601,-5.086601 -13.276619,-5.086601 -18.36322,0 l -8.452714,8.452714 c -5.086602,5.086602 -5.086602,13.276619 0,18.36322 L 40.717186,68.265944 A 74.404572,74.404572 0 0 0 33.052021,86.770757 H 18.847203 c -7.193541,0 -12.9847371,5.791197 -12.9847371,12.984737 V 111.7098 c 0,7.19354 5.7911961,12.98474 12.9847371,12.98474 h 14.204818 a 74.404572,74.404572 0 0 0 7.665165,18.50481 L 30.672838,153.2437 c -5.086602,5.0866 -5.086602,13.27662 0,18.36322 l 8.452714,8.45323 c 5.086601,5.0866 13.276619,5.0866 18.36322,0 L 67.53312,170.01581 a 74.404572,74.404572 0 0 0 18.504814,7.66464 v 14.20482 c 0,7.19354 5.791196,12.98474 12.984737,12.98474 h 11.954309 c 7.19354,0 12.98474,-5.7912 12.98474,-12.98474 v -14.20482 a 74.404572,74.404572 0 0 0 18.50481,-7.66464 l 10.04435,10.04434 c 5.0866,5.0866 13.27662,5.0866 18.36322,0 l 8.45323,-8.45323 c 5.0866,-5.0866 5.0866,-13.27662 0,-18.36322 l -10.04435,-10.04435 a 74.404572,74.404572 0 0 0 7.66465,-18.50481 h 14.20482 c 7.19354,0 12.98474,-5.7912 12.98474,-12.98474 V 99.755494 c 0,-7.19354 -5.7912,-12.984737 -12.98474,-12.984737 h -14.20482 a 74.404572,74.404572 0 0 0 -7.66465,-18.504813 l 10.04435,-10.044349 c 5.0866,-5.086601 5.0866,-13.276618 0,-18.36322 l -8.45323,-8.452714 c -5.0866,-5.086601 -13.27662,-5.086601 -18.36322,0 L 142.46653,41.450009 A 74.404572,74.404572 0 0 0 123.96172,33.784844 V 19.580026 c 0,-7.19354 -5.7912,-12.9847367 -12.98474,-12.9847367 z M 105.00008,50.084108 A 55.648849,55.648849 0 0 1 141.43094,63.666741 74.404572,74.404572 0 0 0 129.05856,62.630629 74.404572,74.404572 0 0 0 54.976781,130.11488 55.648849,55.648849 0 0 1 49.351284,105.73291 55.648849,55.648849 0 0 1 105.00008,50.084108 Z");

    private static Geometry Question => _question ??= Geometry.Parse(
        "F1 m 124.51718,71.85813 c 0,5.159705 -3.81369,11.21675 -8.52473,15.70345 -4.71103,4.4867 -12.33842,8.300395 -17.273791,14.58178 -7.403056,9.6464 -6.281381,20.86315 -6.281381,27.81754 v 2.24335 c 0,2.46768 2.019016,4.4867 4.486701,4.4867 h 22.433501 c 2.46768,0 4.4867,-2.01902 4.4867,-4.4867 v -2.24335 c 0,-10.54375 1.79468,-14.35744 10.31941,-20.63882 9.87074,-7.17872 21.7605,-20.41449 21.7605,-39.034295 0,-26.471531 -20.86316,-45.315672 -47.33469,-45.315672 -26.022862,0 -47.559022,21.536161 -47.559022,48.007692 v 3.365025 c 0,2.467685 2.019015,4.4867 4.4867,4.4867 h 22.4335 c 2.467685,0 4.4867,-2.019015 4.4867,-4.4867 v -3.813695 c 0,-8.524731 6.057046,-16.376456 16.152122,-16.376456 8.9734,0 15.92778,6.057045 15.92778,15.703451 z");

    // resources/Cog.svg: one path, the cog with a round hole.
    private static Geometry Cog => _cog ??= Geometry.Parse(
        "m 98.586005,13.540085 a 3.765719,3.765719 0 0 0 -3.719153,3.177584 l -2.286165,14.455965 c -0.277497,1.754925 -1.921608,3.426837 -3.654041,3.820439 -1.809629,0.411141 -3.603114,0.891565 -5.375899,1.440222 -1.697162,0.525253 -3.051207,1.019059 -3.051493,1.019059 L 69.267898,23.576682 a 3.7656793,3.7656793 0 0 0 -4.809526,-0.891936 l -11.109399,6.413562 a 3.7657248,3.7657248 0 0 0 -1.632458,4.611605 l 5.248775,13.662215 c 0.637145,1.658558 0.0488,3.928487 -1.254704,5.135604 -1.361726,1.261036 -2.674641,2.573951 -3.935677,3.935677 -1.207118,1.303501 -3.477046,1.891849 -5.135604,1.254704 L 32.97709,52.449338 a 3.7657247,3.7657247 0 0 0 -4.611605,1.632458 l -6.413562,11.1094 a 3.7656794,3.7656794 0 0 0 0.891935,4.809525 l 11.37605,9.207707 2.500623,2.024166 c -1.567612,4.823927 -2.048141,6.617247 -2.459282,8.426876 -0.393602,1.732434 -2.065513,3.376543 -3.820438,3.65404 l -14.455966,2.286166 a 3.7657193,3.7657193 0 0 0 -3.177583,3.719153 v 12.828161 a 3.76572,3.76572 0 0 0 3.177583,3.71915 l 14.455966,2.28616 c 1.754925,0.27749 3.426835,1.92161 3.820438,3.65404 0.41114,1.80963 0.891565,3.60311 1.440222,5.3759 0.525253,1.69716 -0.100494,3.95737 -1.481563,5.07515 l -11.37605,9.20719 a 3.7657304,3.7657304 0 0 0 -0.891935,4.80952 l 6.413562,11.10992 a 3.7656746,3.7656746 0 0 0 4.611605,1.63246 l 13.662215,-5.24878 c 1.658558,-0.63714 3.928444,-0.0487 5.135604,1.25471 1.260999,1.36159 2.574005,2.67424 3.935677,3.93516 1.303536,1.20708 1.891849,3.47705 1.254704,5.1356 l -5.248775,13.66221 a 3.7657254,3.7657254 0 0 0 1.632458,4.61161 l 11.109399,6.41356 a 3.7657509,3.7657509 0 0 0 4.810043,-0.89193 l 9.20719,-11.37605 c 1.117799,-1.38105 3.378002,-2.0069 5.075142,-1.48157 1.772804,0.54874 3.566244,1.02951 5.375899,1.44074 1.732418,0.39367 3.376537,2.06552 3.654041,3.82044 l 2.286165,14.45545 a 3.7657338,3.7657338 0 0 0 3.719153,3.17758 h 12.828155 a 3.7657347,3.7657347 0 0 0 3.71916,-3.17758 l 2.28616,-14.45545 c 0.27751,-1.75492 1.92163,-3.42677 3.65404,-3.82044 1.80966,-0.41123 3.6031,-0.892 5.3759,-1.44074 1.69714,-0.52533 3.95686,0.1005 5.07463,1.48157 l 9.2077,11.37605 a 3.7657287,3.7657287 0 0 0 4.80953,0.89193 l 11.10991,-6.41356 a 3.7655864,3.7655864 0 0 0 1.63195,-4.61161 l -5.24826,-13.66221 c -0.63718,-1.65855 -0.0493,-3.92848 1.25419,-5.1356 1.36173,-1.26104 2.67463,-2.57396 3.93567,-3.93568 1.20711,-1.3035 3.47706,-1.89137 5.13561,-1.25419 l 13.66221,5.24826 a 3.7655891,3.7655891 0 0 0 4.61161,-1.63194 l 6.41356,-11.10992 a 3.7657292,3.7657292 0 0 0 -0.89194,-4.80952 l -11.37605,-9.20719 c -1.38107,-1.11778 -2.00688,-3.37801 -1.48156,-5.07515 0.54875,-1.7728 1.02951,-3.56624 1.44074,-5.3759 0.39367,-1.73241 2.06552,-3.37653 3.82044,-3.65404 l 14.45545,-2.28616 a 3.7657344,3.7657344 0 0 0 3.17758,-3.71915 V 99.318829 a 3.7657337,3.7657337 0 0 0 -3.17758,-3.719153 L 179.55936,93.31351 c -1.75492,-0.277504 -3.42677,-1.921622 -3.82044,-3.65404 -0.41123,-1.809656 -0.89199,-3.603095 -1.44074,-5.375899 -0.52532,-1.697139 0.10051,-3.957344 1.48156,-5.075143 l 11.37605,-9.20719 a 3.7657509,3.7657509 0 0 0 0.89194,-4.810042 l -6.41356,-11.1094 a 3.7657254,3.7657254 0 0 0 -4.61161,-1.632458 l -13.66221,5.248775 c -1.65855,0.637145 -3.92853,0.04883 -5.13561,-1.254704 -1.26092,-1.361672 -2.57357,-2.674679 -3.93516,-3.935677 -1.30346,-1.20716 -1.89184,-3.477046 -1.2547,-5.135604 l 5.24877,-13.662215 a 3.7656737,3.7656737 0 0 0 -1.63246,-4.611605 l -11.10991,-6.413562 a 3.7657298,3.7657298 0 0 0 -4.80953,0.891936 l -9.2077,11.37605 c -1.11777,1.38107 -3.37747,2.006816 -5.07463,1.481563 -1.77279,-0.548657 -3.56627,-1.029081 -5.3759,-1.440222 -1.73243,-0.393602 -3.37655,-2.065514 -3.65404,-3.820439 l -2.28616,-14.455965 a 3.7657199,3.7657199 0 0 0 -3.71916,-3.177584 z m 6.414075,52.554932 A 39.638039,39.638039 0 0 1 144.63797,105.73291 39.638039,39.638039 0 0 1 105.00008,145.3708 39.638039,39.638039 0 0 1 65.362193,105.73291 39.638039,39.638039 0 0 1 105.00008,66.095017 Z");

    private static Geometry? _cog;
    private static Geometry? _bevel;
    private static Geometry? _flag_cloth;
    private static Geometry? _bomb;
    private static Geometry? _question;
}
