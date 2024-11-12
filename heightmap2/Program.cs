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
using Mutagen.Bethesda.Plugins.Records;
using FluentResults;
using NexusMods.Paths;
using System.Security.Policy;
using static ICSharpCode.SharpZipLib.Zip.ExtendedUnixData;
using static System.Windows.Forms.Design.AxImporter;

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
        public double baseHeight { get; set; }
        public sbyte[] rawHeight { get; set; } = new sbyte[1089];
        public sbyte[] normalList { get; set; } = new sbyte[3267]; // 33x33x3 bytes
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

        public static Bitmap worldImage = new Bitmap(5000, 5000);
        public static int newWorldSize = 3500;
        public static int lowestCellX = 0;
        public static int lowestCellY = 0;
        public static int highestCellX = 0;
        public static int highestCellY = 0;
        public static int cellSize = 32;
        public static int size = 33;
        public static int imageWidth = 0;
        public static int imageHeight = 0;
        public static double minHeight = -8192;
        public static double maxHeight = 8192;
        public static IFormLinkGetter<IWorldspaceGetter> TamrielWorldspace = Skyrim.Worldspace.Tamriel;
        public static ModKey baseSkyrim = ModKey.FromNameAndExtension("Skyrim.esm");
        public static IGameEnvironment<ISkyrimMod, ISkyrimModGetter> env = GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE);

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            Console.WriteLine("run");

            RunWorldCreator();

            CreateNewCells(state);
        }

        public static void RunWorldCreator()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GeneratorSettingsForm());
        }

        public static void CreateNewCells(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            // Read the image into a 2D array of heights
            double[,] heights = ReadExistingHeightmapImage();
            // Calculate the number of cells
            int numCellsX = (imageWidth - 1) / cellSize;
            int numCellsY = (imageHeight - 1) / cellSize;

            List<Celldata> cellMatrix = new List<Celldata>();

            int midpointX = numCellsX / 2;
            int midpointY = numCellsY / 2;

            int highestx = int.MinValue;
            int highesty = int.MinValue;
            int lowestx = int.MaxValue;
            int lowesty = int.MaxValue;

            // Dictionaries to store edges
            var cellBottomEdges = new Dictionary<(int cellX, int cellY), double[]>();
            var cellRightEdges = new Dictionary<(int cellX, int cellY), double[]>();

            var cellBottomNormals = new Dictionary<(int cellX, int cellY), sbyte[]>();
            var cellRightNormals = new Dictionary<(int cellX, int cellY), sbyte[]>();

            // Process cells from top to bottom
            for (int cellY = 0; cellY < numCellsY; cellY++)
            {
                for (int cellX = 0; cellX < numCellsX; cellX++)
                {
                    double[,] cellHeights = new double[33, 33];

                    int gridPosX = cellX - midpointX;
                    int gridPosY = cellY - midpointY;

                    // Adjust bounds
                    if (gridPosX < lowestx) lowestx = gridPosX;
                    if (gridPosX > highestx) highestx = gridPosX;
                    if (gridPosY < lowesty) lowesty = gridPosY;
                    if (gridPosY > highesty) highesty = gridPosY;

                    // ... (copy heights from the image)

                    for (int j = 0; j <= 32; j++) // y-direction
                    {
                        for (int i = 0; i <= 32; i++) // x-direction
                        {
                            int x = cellX * cellSize + i;
                            int y = cellY * cellSize + j;

                            if (x >= imageWidth) x = imageWidth - 1;
                            if (y >= imageHeight) y = imageHeight - 1;

                            cellHeights[i, j] = heights[x, y];
                        }
                    }

                    // Adjust the leftmost column if there is a cell to the west
                    if (cellX > 0)
                    {
                        var westCellRightEdge = cellRightEdges[(cellX - 1, cellY)];
                        for (int j = 0; j <= 32; j++)
                        {
                            cellHeights[0, j] = westCellRightEdge[j];
                        }
                    }

                    // Adjust the top row if there is a cell to the north
                    if (cellY > 0)
                    {
                        var northCellBottomEdge = cellBottomEdges[(cellX, cellY - 1)];
                        for (int i = 0; i <= 32; i++)
                        {
                            cellHeights[i, 0] = northCellBottomEdge[i];
                        }
                    }

                    // Recalculate the Offset after adjustments
                    double Offset = cellHeights[0, 0];

                    // Compute VHGT data
                    var rawHeight = ComputeVHGTData(cellHeights, Offset);

                    // Compute VNML data
                    var normalList = ComputeVNMLData(cellHeights);

                    // Store the bottom edge and right edge for adjacent cells
                    double[] bottomEdge = new double[33];
                    double[] rightEdge = new double[33];
                    for (int i = 0; i <= 32; i++)
                    {
                        bottomEdge[i] = cellHeights[i, 32];
                    }
                    for (int j = 0; j <= 32; j++)
                    {
                        rightEdge[j] = cellHeights[32, j];
                    }
                    cellBottomEdges[(cellX, cellY)] = bottomEdge;
                    cellRightEdges[(cellX, cellY)] = rightEdge;


                    // Adjust normals along edges
                    // Left edge
                    if (cellX > 0)
                    {
                        var key = (cellX - 1, cellY);
                        if (!cellRightNormals.ContainsKey(key))
                        {
                            Console.WriteLine($"cellRightNormals does not contain key {key}");
                            throw new KeyNotFoundException($"cellRightNormals does not contain key {key}");
                        }

                        var westCellRightNormals = cellRightNormals[key];
                        for (int j = 0; j < size; j++)
                        {
                            int indexCurrent = (j * size + 0) * 3;
                            int edgeIndex = j * 3;

                            normalList[indexCurrent] = westCellRightNormals[edgeIndex];
                            normalList[indexCurrent + 1] = westCellRightNormals[edgeIndex + 1];
                            normalList[indexCurrent + 2] = westCellRightNormals[edgeIndex + 2];
                        }
                    }

                    // Top edge
                    if (cellY > 0)
                    {
                        var key = (cellX, cellY - 1);
                        if (!cellBottomNormals.ContainsKey(key))
                        {
                            Console.WriteLine($"cellBottomNormals does not contain key {key}");
                            throw new KeyNotFoundException($"cellBottomNormals does not contain key {key}");
                        }

                        var northCellBottomNormals = cellBottomNormals[key];
                        for (int i = 0; i < size; i++)
                        {
                            int indexCurrent = (0 * size + i) * 3;
                            int edgeIndex = i * 3;

                            normalList[indexCurrent] = northCellBottomNormals[edgeIndex];
                            normalList[indexCurrent + 1] = northCellBottomNormals[edgeIndex + 1];
                            normalList[indexCurrent + 2] = northCellBottomNormals[edgeIndex + 2];
                        }
                    }

                    sbyte[] bottomEdgeNormals = new sbyte[size * 3];

                    for (int i = 0; i < size; i++)
                    {

                        int indexCurrent = ((size - 1) * size + i) * 3;
                        int edgeIndex = i * 3;
                        bottomEdgeNormals[edgeIndex] = normalList[indexCurrent];
                        bottomEdgeNormals[edgeIndex + 1] = normalList[indexCurrent + 1];
                        bottomEdgeNormals[edgeIndex + 2] = normalList[indexCurrent + 2];
                    }
                    cellBottomNormals[(cellX, cellY)] = bottomEdgeNormals;


                    // Store right edge  normals
                    sbyte[] rightEdgeNormals = new sbyte[size * 3];
                    for (int j = 0; j < size; j++)
                    {

                        int indexCurrent = (j * size + (size - 1)) * 3;
                        int edgeIndex = j * 3;
                        rightEdgeNormals[edgeIndex] = normalList[indexCurrent];
                        rightEdgeNormals[edgeIndex + 1] = normalList[indexCurrent + 1];
                        rightEdgeNormals[edgeIndex + 2] = normalList[indexCurrent + 2];
                    }
                    cellRightNormals[(cellX, cellY)] = rightEdgeNormals;

                    // Create Celldata object
                    Celldata matrixcell = new Celldata
                    {
                        gridpos = new Point(gridPosX, gridPosY),
                        baseHeight = Offset,
                        rawHeight = rawHeight,
                        normalList = normalList
                    };

                    cellMatrix.Add(matrixcell);
                }
            }

            
            Worldspace worldspace = AddToSkyrimPatch(highestx, lowestx, highesty, lowesty, cellMatrix, state);
            WorldSpaceHeightmapToImage(worldspace, state);
            Console.WriteLine("Heightmap import completed successfully.");
        }
        public static Worldspace AddToSkyrimPatch(int highestx, int lowestx, int highesty, int lowesty, List<Celldata> cellMatrix, IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {

            // Create the new worldspace
            var worldspace = state.PatchMod.Worldspaces.AddNew();
            worldspace.EditorID = "js_new_worldspace";
            worldspace.Name = "Custom Worldspace";
            worldspace.Water.SetTo(Skyrim.Water.DefaultWater);
            worldspace.LodWater.SetTo(Skyrim.Water.DefaultWater);
            worldspace.LodWaterHeight = -14000.000000f;
            worldspace.LandDefaults = new WorldspaceLandDefaults();
            worldspace.LandDefaults.DefaultLandHeight = 10.000000f;
            worldspace.LandDefaults.DefaultWaterHeight = -14000.000000f;
            worldspace.DistantLodMultiplier = 1.0f;
            worldspace.ObjectBoundsMax = new P2Float(highestx, highesty);
            worldspace.ObjectBoundsMin = new P2Float(lowestx, lowesty);


            foreach (Celldata matrixcell in cellMatrix)
            {
                Mutagen.Bethesda.Skyrim.Cell newcell = new Mutagen.Bethesda.Skyrim.Cell(state.PatchMod);
                newcell.Flags |= Mutagen.Bethesda.Skyrim.Cell.Flag.HasWater;

                newcell.Name = "js_" + matrixcell.gridpos.X + "." + matrixcell.gridpos.Y;
                newcell.EditorID = "js_" + matrixcell.gridpos.X + "." + matrixcell.gridpos.Y;


                P2Int newgrid = new P2Int
                {
                    X = matrixcell.gridpos.X,
                    Y = matrixcell.gridpos.Y
                };
                newcell.Grid = new CellGrid
                {
                    Point = newgrid
                };
                newcell.Landscape = new Mutagen.Bethesda.Skyrim.Landscape(state.PatchMod);
                newcell.Landscape.Flags = new Mutagen.Bethesda.Skyrim.Landscape.Flag();
                
                LandscapeVertexHeightMap newheightvertexes = new LandscapeVertexHeightMap();
                IArray2d<byte> _HeightMap = new Array2d<byte>(33, 33, 0);

                // Convert rawHeight (sbyte[]) to _HeightMap (IArray2d<byte>)
                int index = 0;
                for (int j = 0; j <= 32; j++)
                {
                    for (int i = 0; i <= 32; i++)
                    {
                        sbyte value = matrixcell.rawHeight[index++];
                        _HeightMap[i, j] = unchecked((byte)value);
                    }
                }

                newheightvertexes.HeightMap.SetTo(_HeightMap);
                newheightvertexes.Offset = (float)matrixcell.baseHeight;
                newcell.Landscape.VertexHeightMap = newheightvertexes;
                newcell.Landscape.Flags |= Mutagen.Bethesda.Skyrim.Landscape.Flag.VertexNormalsHeightMap; 
                bool hasVertexNormalsHeightMap = (newcell.Landscape.Flags & Mutagen.Bethesda.Skyrim.Landscape.Flag.VertexNormalsHeightMap) == Mutagen.Bethesda.Skyrim.Landscape.Flag.VertexNormalsHeightMap;
                if (hasVertexNormalsHeightMap) { 
                    Console.WriteLine("Added VertexNormalsHeightMap flag to landscape"); 
                } else { 
                    Console.WriteLine("It ain't there"); 
                }


                // Convert sbyte[] normalList to IArray2d<P3UInt8>
                var normalsArray2D = new Array2d<P3UInt8>(size, size, new P3UInt8(0, 0, 0));

                int normalindex = 0;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        sbyte snx = matrixcell.normalList[normalindex++];
                        sbyte sny = matrixcell.normalList[normalindex++];
                        sbyte snz = matrixcell.normalList[normalindex++];

                        // Convert sbyte (-128 to 127) to byte (0 to 255)
                        byte nx = (byte)(snx + 128);
                        byte ny = (byte)(sny + 128);
                        byte nz = (byte)(snz + 128);

                        normalsArray2D[x, y] = new P3UInt8(nx, ny, nz);
                    }
                }

                newcell.Landscape.VertexNormals = normalsArray2D;

                worldspace.AddCell(newcell);
            }

            return worldspace;
        }

        public static double[,] ReadExistingHeightmapImage()
        {
            string imagePath = @"planetgenerated.png";
            Bitmap newHeightImage = (Bitmap)Bitmap.FromFile(imagePath);

            imageWidth = newHeightImage.Width;
            imageHeight = newHeightImage.Height;

            // Calculate the number of cells
            int numCellsX = (imageWidth - 1) / cellSize;
            int numCellsY = (imageHeight - 1) / cellSize;

            // Read the image into a 2D array of heights
            double[,] heights = new double[imageWidth, imageHeight];

            for (int y = 0; y < imageHeight; y++)
            {
                for (int x = 0; x < imageWidth; x++)
                {
                    System.Drawing.Color pixelColor = newHeightImage.GetPixel(x, y);
                    float pixelValue = pixelColor.R; // Assuming grayscale image
                    double height = minHeight + (pixelValue / 255f) * (maxHeight - minHeight);
                    heights[x, y] = height;
                }
            }
            return heights;
        }

        public static sbyte[] ComputeVHGTData(double[,] cellHeights, double Offset)
        {
            sbyte[] rawHeight = new sbyte[1089]; // 33x33 grid
            int index = 0;

            // The first value is always zero since it's the starting point
            rawHeight[index++] = 0;

            // Leftmost column
            for (int j = 1; j <= 32; j++)
            {
                double diff = (cellHeights[0, j] - cellHeights[0, j - 1]) / 8.0f;
                int delta = (int)Math.Round(diff);
                delta = Math.Clamp(delta, -128, 127);
                rawHeight[index++] = (sbyte)delta;
            }

            // Rest of the grid
            for (int j = 0; j <= 32; j++)
            {
                for (int i = 1; i <= 32; i++)
                {
                    double diff = (cellHeights[i, j] - cellHeights[i - 1, j]) / 8.0f;
                    int delta = (int)Math.Round(diff);
                    delta = Math.Clamp(delta, -128, 127);
                    rawHeight[index++] = (sbyte)delta;
                }
            }

            return rawHeight;
        }

        public static sbyte[] ComputeVNMLData(double[,] cellHeights)
        {
            int size = 33;
            sbyte[] normalList = new sbyte[size * size * 3];
            int index = 0;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Calculate gradients (dx, dy)
                    double dx, dy;

                    if (x > 0 && x < size - 1)
                        dx = (cellHeights[x + 1, y] - cellHeights[x - 1, y]) / 2.0;
                    else
                        dx = 0.0;

                    if (y > 0 && y < size - 1)
                        dy = (cellHeights[x, y + 1] - cellHeights[x, y - 1]) / 2.0;
                    else
                        dy = 0.0;

                    // Normal vector components
                    double nx = -dx;
                    double ny = -dy;
                    double nz = 1.0;

                    // Normalize the vector
                    double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    nx /= length;
                    ny /= length;
                    nz /= length;

                    // Convert to sbyte range (-127 to +127)
                    sbyte snx = (sbyte)(nx * 127);
                    sbyte sny = (sbyte)(ny * 127);
                    sbyte snz = (sbyte)(nz * 127);

                    // Store in the normal list
                    normalList[index++] = snx;
                    normalList[index++] = sny;
                    normalList[index++] = snz;
                }
            }

            return normalList;
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

        // Remap function
        static double Remap(double value, double fromMin, double fromMax, double toMin, double toMax)
        {
            double fromAbs = value - fromMin;
            double fromMaxAbs = fromMax - fromMin;
            double normal = fromAbs / fromMaxAbs;
            double toMaxAbs = toMax - toMin;
            double toAbs = toMaxAbs * normal;
            return toAbs + toMin;
        }

        public static void WorldSpaceHeightmapToImage(Worldspace targetWorldspace, IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            Dictionary<FormKey, ICellGetter> _accumulatedCells = new();
            List<Celldata> cellMatrix = new List<Celldata>();
            Console.WriteLine("zero");
            foreach (var subcell in targetWorldspace.SubCells)
            {
                foreach (var subblock in subcell.Items)
                {
                    foreach (var cell in subblock.Items)
                    {
                        ParseCell(cell, ref _accumulatedCells, ref cellMatrix);
                    }
                }
            }

            // Calculate the image dimensions
            int cellSize = 33;

            imageWidth = (highestCellX - lowestCellX + 1) * (cellSize - 1) + 500;
            imageHeight = (highestCellY - lowestCellY + 1) * (cellSize - 1) + 500;

            Bitmap worldImage = new Bitmap(imageWidth, imageHeight);
            Console.WriteLine("imageWidth = " + imageWidth);
            Console.WriteLine("imageHeight= " + imageHeight);
            Console.WriteLine("lowestx= " + lowestCellX);
            Console.WriteLine("highesstx= " + highestCellX);
            Console.WriteLine("lowesty= " + lowestCellY);
            Console.WriteLine("highesty= " + highestCellY);
            var cellcounter = 0;
            foreach (var celldata in cellMatrix)
            {
                Point gridPos = celldata.gridpos;
                sbyte[] rawHeight = celldata.rawHeight;
                double baseHeight = celldata.baseHeight;
                cellcounter += 1;

                // Reconstruct the heights
                double[,] heights = new double[cellSize, cellSize];
                int index = 0;
                heights[0, 0] = baseHeight;

                index++; // Skip the first delta, which is zero

                // Leftmost column
                for (int y = 1; y < cellSize; y++)
                {
                    sbyte delta = rawHeight[index++];
                    heights[0, y] = heights[0, y - 1] + delta * 8.0f;
                }

                // Rest of the grid
                for (int y = 0; y < cellSize; y++)
                {
                    for (int x = 1; x < cellSize; x++)
                    {
                        sbyte delta = rawHeight[index++];
                        heights[x, y] = heights[x - 1, y] + delta * 8.0f;
                    }
                }

                // Adjust start position
                Point startPos = new Point(
                    (gridPos.X - lowestCellX) * (cellSize - 1),
                    (gridPos.Y - lowestCellY) * (cellSize - 1)
                );
                // Draw to the bitmap
                for (int x = 0; x < 32; x++)
                {
                    for (int y = 0; y < 32; y++)
                    {
                        double heightValue = heights[x, y];

                        int colorValue = (int)Remap(heightValue, minHeight, maxHeight, 0, 65535);
                        colorValue = Math.Clamp(colorValue, 0, 65535);
                        int red = (colorValue >> 8) & 0xFF;
                        int green = (colorValue >> 8) & 0xFF;
                        int blue = (colorValue >> 8) & 0xFF;
                        System.Drawing.Color color = System.Drawing.Color.FromArgb(red, green, blue);

                        int posX = startPos.X + x;
                        int posY = startPos.Y + y; // No need to flip y-coordinate

                        worldImage.SetPixel(posX, posY, color);
                    }
                }
            }
            Console.WriteLine("cell counter = " + cellcounter);
            // Save the image
            worldImage.Save("heightmap.png", ImageFormat.Png);
            Console.WriteLine("Heightmap saved as heightmap.png");
        }

        public static void ParseCell(ICellGetter cell, ref Dictionary<FormKey, ICellGetter> _accumulatedCells, ref List<Celldata> cellMatrix)
        {

            if (!_accumulatedCells.ContainsKey(cell.FormKey) && !cell.Flags.HasFlag(Mutagen.Bethesda.Skyrim.Cell.Flag.IsInteriorCell))
            {
                if (cell.Grid == null || cell.Landscape == null || cell.Landscape.VertexHeightMap == null)
                {
                    if (cell.Grid == null)
                    {
                        Console.WriteLine("cellgrid is null");
                    }
                    if (cell.Landscape == null)
                    {
                        Console.WriteLine("Landscape is null");
                        return;
                    }
                    if (cell.Landscape.VertexHeightMap == null)
                    {
                        Console.WriteLine("vertexheightmap is null");
                    }
                    return;
                }

                Console.WriteLine("cellname = " + cell.Name);
                var heightdata = cell.Landscape.VertexHeightMap;
                var rawoffset = heightdata.Offset;
                var rawheight = heightdata.HeightMap;
                var normaldata = cell.Landscape.VertexNormals;
                var griddata = cell.Grid;
                SetLimits(griddata);
                var rawheightarray = rawheight.ToArray();

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

                newcell.gridpos = new Point(griddata.Point.X, griddata.Point.Y);
                newcell.baseHeight = rawoffset;
                if (cell.Name != null && cell.Name.String != null)
                {
                    newcell.name = cell.Name.String;
                }
                cellMatrix.Add(newcell);

            }
        }

    }
}