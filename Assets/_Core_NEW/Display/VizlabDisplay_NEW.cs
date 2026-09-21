using UnityEngine;

/// <summary>
/// The Vizlab display's physical geometry, in one place, so that every editor aid in this
/// folder measures against the same surface.
///
/// WHAT THE SURFACE IS: seven stacked panels, each 5129.6 x 575.3 mm. It curves only
/// vertically - every panel's rotation is about X alone - so left-to-right the surface is
/// flat and there is no horizontal wrap. Each row is therefore a plain flat rectangle, and
/// the "curve" is seven flat facets stepping 18 degrees apart over a 123.7 degree arc.
/// That is why placement, not scaling, is what a UI element needs to get right.
///
/// Row 1 is the top, nearly overhead at 78 degrees. Row 7 is the floor row. Rows 1 to 6 sit
/// on a circle; row 7 is the exception and tilts back only 25.5 degrees instead of taking
/// the 18-degree step, flattening toward the floor.
///
/// WHERE THE NUMBERS CAME FROM: reconstructed from Chris's vizlabDisplay.ipynb summary
/// (per-row tilt, per-row vertical extent, and the fitted circle) rather than copied from
/// its corner table, which is not in the repo yet. The reconstruction reproduces every
/// figure that summary states: each row's height range to under 2 mm, the inter-row gap to
/// 5.33-5.36 mm against a stated 5.3-5.4, and the total surface run to 4059.2 mm against a
/// stated 4060. It is accurate enough to lay out against.
///
/// TO REPLACE IT WITH GROUND TRUTH: the only authored values are the seven Rows entries
/// below. Swap their centres and tilts for ones derived from the real corner table and
/// everything downstream follows. Nothing else in this folder hardcodes geometry.
///
/// UNCONFIRMED: RowWidthPx is 5 x 1920 from meeting notes, not from the file. Every pixel
/// figure here depends on it. The row *fractions* used for UI anchoring do not, which is
/// why VizlabRowAnchor_NEW works in normalised space and stays correct either way.
/// </summary>
public static class VizlabDisplay_NEW
{
    // ---- Panel size, millimetres -------------------------------------------------

    public const float RowWidthMm = 5129.6f;
    public const float RowHeightMm = 575.3f;

    public const int RowCount = 7;

    // ---- Pixels ------------------------------------------------------------------

    /// <summary>UNCONFIRMED: 5 panels x 1920, from meeting notes rather than the file.</summary>
    public const int RowWidthPx = 9600;
    public const int RowHeightPx = 1080;

    public const int CanvasWidthPx = RowWidthPx;               // 9600
    public const int CanvasHeightPx = RowHeightPx * RowCount;  // 7560

    /// <summary>1.8715 across vs 1.8773 down: a 0.3% difference, so treat pixels as square.</summary>
    public const float PixelsPerMmX = RowWidthPx / RowWidthMm;
    public const float PixelsPerMmY = RowHeightPx / RowHeightMm;

    // ---- Viewing position, millimetres -------------------------------------------

    /// <summary>
    /// Centre of the circle rows 1-6 sit on. Every point on those rows is the same 1855.7 mm
    /// from here, which is why the arc reads as even from this spot and skews from anywhere
    /// else. OPEN QUESTION for Chris: is this the intended viewing position, or is the file
    /// origin?
    /// </summary>
    public static readonly Vector3 CentreOfCurvatureMm = new Vector3(0f, 1238.8f, -41.1f);

    /// <summary>The file origin. The other candidate viewing position.</summary>
    public static readonly Vector3 FileOriginMm = Vector3.zero;

    /// <summary>
    /// Where a standing adult's eyes actually are, for comparison. This matters more than it
    /// looks: the centre of curvature is at y 1238.8, which is roughly seated height, so a
    /// visitor standing up is NOT at the point the surface was designed around, and the rows
    /// stop being equidistant from them. See GetApparentScale.
    /// </summary>
    public static readonly Vector3 StandingEyeMm = new Vector3(0f, 1600f, 0f);

    // ---- The rows ----------------------------------------------------------------

    /// <summary>One flat panel. Centre is in millimetres; tilt is about X, in degrees.</summary>
    public struct Row
    {
        /// <summary>1 = top, 7 = floor.</summary>
        public readonly int Index;

        /// <summary>Degrees about X. 0 stands vertical facing the viewer; positive leans back overhead.</summary>
        public readonly float TiltDeg;

        /// <summary>Panel centre in millimetres. X is always 0; the surface is symmetric.</summary>
        public readonly Vector3 CentreMm;

        public Row(int index, float tiltDeg, float centreY, float centreZ)
        {
            Index = index;
            TiltDeg = tiltDeg;
            CentreMm = new Vector3(0f, centreY, centreZ);
        }

        /// <summary>Panel-local up, in world terms. For tilt 0 this is straight up.</summary>
        public Vector3 Up
        {
            get
            {
                float r = TiltDeg * Mathf.Deg2Rad;
                return new Vector3(0f, Mathf.Cos(r), Mathf.Sin(r));
            }
        }

        /// <summary>Panel-local right. The surface never rotates about Y or Z, so this is fixed.</summary>
        public Vector3 Right { get { return Vector3.right; } }

        /// <summary>Which way the panel faces. Points back toward the viewer.</summary>
        public Vector3 Normal { get { return Vector3.Cross(Right, Up).normalized; } }
    }

    /// <summary>
    /// Index 0 is row 1, the top. Centres are reconstructed as described in the class
    /// summary: rows 1-6 are placed on the fitted circle at radius 1833.3 mm. The 1855.7 mm
    /// in the summary is the circle through the panel *edges*; panel centres sit inboard of
    /// it by half a panel height, which is what reconciles the two. Row 7 is placed from its
    /// own stated tilt and vertical extent, because it does not lie on that circle.
    ///
    /// KNOWN SOFT SPOT: the six seams come out at 5.31-5.42 mm against a stated 5.3-5.4,
    /// except the row 6 to row 7 seam, which comes out at 7.2 mm. That is the join to the
    /// off-arc floor row, and it is the one number the reconstruction does not land. If the
    /// real corner table ever arrives, that seam is the thing to check first.
    /// </summary>
    public static readonly Row[] Rows =
    {
        new Row(1, 78.0f, 3032.05f, -422.26f),
        new Row(2, 60.0f, 2826.52f, -957.75f),
        new Row(3, 42.0f, 2465.50f, -1403.48f),
        new Row(4, 24.0f, 1984.48f, -1715.82f),
        new Row(5, 6.0f, 1430.41f, -1864.35f),
        new Row(6, -12.0f, 857.64f, -1834.35f),
        new Row(7, -25.5f, 310.00f, -1647.84f),
    };

    /// <summary>Takes 1..7, not a 0-based index. Clamped, so a bad row gives you a panel rather than a throw.</summary>
    public static Row GetRow(int rowNumber)
    {
        return Rows[Mathf.Clamp(rowNumber, 1, RowCount) - 1];
    }

    // ---- Where UI should and should not go ---------------------------------------

    /// <summary>
    /// Row 5 is the only row within 6 degrees of vertical, and it lands at standing eye
    /// height. It is the one place a flat screen-space element stays flat.
    /// </summary>
    public const int SafeAreaRow = 5;

    /// <summary>
    /// Rows 1 and 2 are above the head at 78 and 60 degrees; row 7 breaks from the arc and
    /// lies toward the floor. Nothing critical belongs in any of them.
    /// </summary>
    public static bool IsPoorPlacement(int rowNumber)
    {
        return rowNumber == 1 || rowNumber == 2 || rowNumber == 7;
    }

    // ---- Corners, millimetres ----------------------------------------------------

    /// <summary>
    /// The panel's four corners in millimetres, ordered top-left, top-right, bottom-right,
    /// bottom-left as seen by a viewer facing it.
    /// </summary>
    public static void GetCornersMm(int rowNumber, Vector3[] into)
    {
        Row row = GetRow(rowNumber);
        Vector3 halfAcross = row.Right * (RowWidthMm * 0.5f);
        Vector3 halfUp = row.Up * (RowHeightMm * 0.5f);

        into[0] = row.CentreMm - halfAcross + halfUp;
        into[1] = row.CentreMm + halfAcross + halfUp;
        into[2] = row.CentreMm + halfAcross - halfUp;
        into[3] = row.CentreMm - halfAcross - halfUp;
    }

    /// <summary>Allocating form, for editor code where one array per call does not matter.</summary>
    public static Vector3[] GetCornersMm(int rowNumber)
    {
        var corners = new Vector3[4];
        GetCornersMm(rowNumber, corners);
        return corners;
    }

    // ---- Seams -------------------------------------------------------------------

    /// <summary>
    /// The gap in millimetres between a row and the one below it. Takes the upper row, 1..6.
    ///
    /// Measured from the reconstructed panels rather than assumed, so it reports the 5.3-5.4
    /// mm the summary states for most joins and the wider join to the off-arc floor row.
    /// </summary>
    public static float GetSeamMm(int upperRowNumber)
    {
        int n = Mathf.Clamp(upperRowNumber, 1, RowCount - 1);

        Vector3[] upper = GetCornersMm(n);      // bottom edge is [3] to [2]
        Vector3[] lower = GetCornersMm(n + 1);  // top edge is [0] to [1]

        return Vector3.Distance(upper[3], lower[0]);
    }

    /// <summary>
    /// The same gap in canvas pixels. About 10 px on most joins - small, but a line of text
    /// or a thin rule laid across one will be cut.
    /// </summary>
    public static float GetSeamPx(int upperRowNumber)
    {
        return GetSeamMm(upperRowNumber) * PixelsPerMmY;
    }

    // ---- What a viewer actually sees ---------------------------------------------

    /// <summary>
    /// How large content on this row appears relative to content on the safe-area row, from
    /// a given eye position. 1.0 means identical; 1.25 means a given element reads 25%
    /// bigger there than the same element on row 5.
    ///
    /// WHY THIS IS THE DISTORTION THAT MATTERS: rows 1-6 all sit on a circle centred on the
    /// design viewing position, so from that exact point every row is the same distance away
    /// and nothing is distorted at all - the surface is built so the arc reads evenly. Stand
    /// somewhere else, though, and the rows stop being equidistant. From standing eye height
    /// the nearest row is about 1.49 m away and the farthest about 2.09 m, so the same
    /// element can read 40% larger on one row than another. That difference is scale, not
    /// shear: panels stay very nearly face-on either way, within a few degrees.
    /// </summary>
    public static float GetApparentScale(int rowNumber, Vector3 eyeMm)
    {
        float reference = Vector3.Distance(GetRow(SafeAreaRow).CentreMm, eyeMm);
        float here = Vector3.Distance(GetRow(rowNumber).CentreMm, eyeMm);

        if (here <= Mathf.Epsilon) return 1f;
        return reference / here;
    }

    /// <summary>
    /// The elevation of a row's centre above the viewer's eyeline, in degrees. Positive is
    /// up. This is the neck angle, and it is why rows 1 and 2 are poor places for anything
    /// someone has to read for more than a moment.
    /// </summary>
    public static float GetElevationDeg(int rowNumber, Vector3 eyeMm)
    {
        Vector3 toRow = GetRow(rowNumber).CentreMm - eyeMm;

        // -Z is forward, away from the viewer, so the horizontal run is -toRow.z.
        return Mathf.Atan2(toRow.y, -toRow.z) * Mathf.Rad2Deg;
    }

    // ---- Canvas mapping ----------------------------------------------------------

    /// <summary>
    /// The row's band as fractions of canvas height with 0 at the bottom - the form Unity's
    /// anchors want. Row 1 is the top band, so it maps high.
    ///
    /// This is the one mapping that does not depend on the unconfirmed pixel figures, which
    /// is why the anchoring helper uses it.
    /// </summary>
    public static void GetRowAnchorFractions(int rowNumber, out float yMin, out float yMax)
    {
        int n = Mathf.Clamp(rowNumber, 1, RowCount);
        yMin = (RowCount - n) / (float)RowCount;
        yMax = (RowCount - n + 1) / (float)RowCount;
    }

    /// <summary>
    /// The row's band in canvas pixels, origin bottom-left, for when you want the literal
    /// number. Row 3 comes back as y 4320 to 5400, centre 4860.
    /// </summary>
    public static Rect GetRowCanvasRectPx(int rowNumber)
    {
        int n = Mathf.Clamp(rowNumber, 1, RowCount);
        float yMin = (RowCount - n) * RowHeightPx;
        return new Rect(0f, yMin, CanvasWidthPx, RowHeightPx);
    }
}
