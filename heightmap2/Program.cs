using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Noggog;
using System.Runtime.CompilerServices;
using SharpNoise;
using SharpNoise.Builders;
using SharpNoise.Modules;
using SharpNoise.Utilities.Imaging;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ComplexPlanetExample;

namespace heightmap2
{

    public struct Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
    }

    public class Celldata
    {
        public Point gridpos { get; set; }
        public float baseHeight { get; set; }
        public sbyte[] rawHeight { get; set; } = new sbyte[1089];
        public sbyte[] normalList { get; set; } = new sbyte[1089];
        public string name { get; set; } = " ";
    }

    public class Program
    {

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "YourPatcher.esp")
                .Run(args);
        }

        public static Dictionary<FormKey, ICellGetter> _accumulatedCells = new();
        public static Bitmap worldImage = new Bitmap(5000, 5000);
        public static int newWorldSize = 3500;
        public static int lowestCellX = 0;
        public static int lowestCellY = 0;
        public static int highestCellX = 0;
        public static int highestCellY = 0;
        public static int cellSize = 33;
        public static int imageWidth = 0;
        public static int imageHeight = 0;
        public static List<Celldata> cellMatrix = new List<Celldata>();
        public static List<Celldata> createdCellMatrix = new List<Celldata>();

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var env = GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE);
            Console.WriteLine("run");
            IFormLinkGetter<IWorldspaceGetter> TamrielWorldspace = Skyrim.Worldspace.Tamriel;
            ModKey baseSkyrim = ModKey.FromNameAndExtension("Skyrim.esm");

            foreach (var tamrielOverride in TamrielWorldspace.ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, IWorldspace, IWorldspaceGetter>(env.LinkCache))
            {
                if (tamrielOverride.ModKey != baseSkyrim)
                {
                    continue;
                }
                foreach (var cell in tamrielOverride.Record.EnumerateMajorRecords<ICellGetter>())
                {
                    if (!_accumulatedCells.ContainsKey(cell.FormKey) && !cell.Flags.HasFlag(Mutagen.Bethesda.Skyrim.Cell.Flag.IsInteriorCell))
                    {
                        if (cell.Grid == null || cell.Landscape == null || cell.Landscape.VertexHeightMap == null || cell.Landscape.VertexNormals == null)
                        {
                            continue;
                        }
                        var heightdata = cell.Landscape.VertexHeightMap;
                        var rawoffset = heightdata.Offset;
                        var rawheight = heightdata.HeightMap;
                        var normaldata = cell.Landscape.VertexNormals;
                        var griddata = cell.Grid;
                        SetLimits(griddata);
                        var rawheightarray = rawheight.ToArray();
                        var rawnormals = normaldata.ToArray();

                        _accumulatedCells.Add(cell.FormKey, cell);
                        var newcell = new Celldata();

                        //mutagen stores it as IReadOnlyArray2d of unsigned bytes 0 to 255
                        for (int i = 0; i < rawheightarray.Length; i++)
                        {
                            var indexed = rawheightarray[i];
                            //store as signed 8-bit or unsigned? who knows, but I know the hex matches xedit
                            byte value = indexed.Value;
                            //try signed
                            sbyte sb = unchecked((sbyte)value);

                            //Console.WriteLine(sb.ToString());
                            newcell.rawHeight[i] = sb;
                        }

                        for (int i = 0; i < rawnormals.Length; i++)
                        {
                            var indexed = rawheightarray[i];
                            byte value = indexed.Value;
                            //try signed
                            sbyte sb = unchecked((sbyte)value);
                            newcell.normalList[i] = sb;
                        }
                        newcell.gridpos = new Point(griddata.Point.X, griddata.Point.Y);
                        newcell.baseHeight = rawoffset;
                        if( cell.Name != null && cell.Name.String != null)
                        {
                            newcell.name = cell.Name.String;
                        }
                        cellMatrix.Add(newcell);

                    }
                }
            }
            Console.WriteLine("cellmatrix size = " + cellMatrix.Count);
            ParseCells();

            RunWorldCreator();
        }

        public static void RunWorldCreator()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GeneratorSettingsForm());
        }

        public static void CreateNewCells(SharpNoise.Utilities.Imaging.Image createdImage)
        {
            ;
        }

        public static void SetLimits(ICellGridGetter? grid)
        {
            if (grid == null) { return; }
            if (grid.Point.X < lowestCellX)
            {
                lowestCellX = grid.Point.X;
            }
            if (grid.Point.X > highestCellX)
            {
                highestCellX = grid.Point.X;
            }
            if (grid.Point.Y < lowestCellY)
            {
                lowestCellY = grid.Point.Y;
            }
            if (grid.Point.Y > highestCellY)
            {
                highestCellY = grid.Point.Y;
            }
        }

        static void ParseCells()
        {

            // Calculate the image dimensions
            int cellSize = 33;

            imageWidth = (highestCellX - lowestCellX + 1) * (cellSize - 1) + 500;
            imageHeight = (highestCellY - lowestCellY + 1) * (cellSize - 1) + 500;

            Bitmap worldImage = new Bitmap(imageWidth, imageHeight);
            Console.WriteLine("imageWidth = " + imageWidth);
            Console.WriteLine("imageHeight= " + imageHeight);
            Console.WriteLine("lowestx= " + lowestCellX);
            Console.WriteLine("highesstx= " + highestCellX);
            Console.WriteLine("lowesty= " + lowestCellX);
            Console.WriteLine("highesty= " + lowestCellX);
            var cellcounter = 0;
            foreach (var celldata in cellMatrix)
            {
                Point gridPos = celldata.gridpos;
                sbyte[] heights = celldata.rawHeight;
                float[,] _raw = new float[cellSize, cellSize];
                float baseheight = celldata.baseHeight;
                cellcounter += 1;
                // Fill _raw array
                for (int i = 0; i < cellSize * cellSize; i++)
                {
                    int y = i / cellSize;
                    int x = i % cellSize;
                    _raw[x, y] = heights[i];
                }

                float[,] rawPoints = new float[cellSize, cellSize];
                float runningOffsetY = 0;
                for (int y = 1; y < cellSize; y++)
                {
                    runningOffsetY += _raw[0, y];
                    float runningOffsetX = 0;
                    for (int x = 1; x < cellSize; x++)
                    {
                        runningOffsetX += _raw[x, y];
                        rawPoints[x - 1, y - 1] = baseheight + (_raw[x, y] + runningOffsetY + runningOffsetX);
                    }
                }

                Point startPos = new Point((gridPos.X - lowestCellX) * (cellSize - 1), (highestCellY - gridPos.Y) * (cellSize - 1));

                // Draw to the bitmap
                for (int x = 0; x < 32; x++)
                {
                    for (int y = 0; y < 32; y++)
                    {
                        float heightValue = rawPoints[x, y];
                        int colorValue = (int)Remap(heightValue, -5000f, 5000f, 0f, 255f);
                        colorValue = Math.Clamp(colorValue, 0, 255);
                        System.Drawing.Color color = System.Drawing.Color.FromArgb(colorValue, colorValue, colorValue);

                        // Flip the y-coordinate
                        int flippedY = (cellSize) - y;

                        if ( startPos.Y + flippedY > imageHeight  || startPos.Y + flippedY < 0)
                        {
                            Console.WriteLine("gridpos = " + gridPos.X + " " + gridPos.Y);
                            Console.WriteLine("startPos.Y = " + startPos.Y);
                            Console.WriteLine("y = " + y);
                        }
                        worldImage.SetPixel(startPos.X + x, startPos.Y + flippedY, color);
                    }
                }
            }
            Console.WriteLine("cell counter = " + cellcounter);
            // Save the image
            worldImage.Save("heightmap.png", ImageFormat.Png);
            Console.WriteLine("Heightmap saved as heightmap.png");
        }

        // Remap function
        static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float fromAbs = value - fromMin;
            float fromMaxAbs = fromMax - fromMin;
            float normal = fromAbs / fromMaxAbs;
            float toMaxAbs = toMax - toMin;
            float toAbs = toMaxAbs * normal;
            return toAbs + toMin;
        }

        // Function to remap values
        static float Remap_two(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float fromAbs = value - fromMin;
            float fromMaxAbs = fromMax - fromMin;
            float normal = fromAbs / fromMaxAbs;
            float toMaxAbs = toMax - toMin;
            float toAbs = toMaxAbs * normal;
            float to = toAbs + toMin;
            return Math.Max(toMin, Math.Min(toMax, to));
        }
    }
}
