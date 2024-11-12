using System.ComponentModel;

namespace ComplexPlanetExample
{
    /// <summary>
    /// Contains all parameters for planet surface generation
    /// </summary>
    public class PlanetGenerationSettings
    {
        /// <summary>
        /// Southernmost coordinate of elevation grid.
        /// </summary>
        [Description("Southernmost coordinate of elevation grid")]
        [Category("Coordinates")]
        public double SouthCoord { get; set; }

        /// <summary>
        /// Northernmost coordinate of elevation grid.
        /// </summary>
        [Description("Northernmost coordinate of elevation grid.")]
        [Category("Coordinates")]
        public double NorthCoord { get; set; }

        /// <summary>
        /// Westernmost coordinate of elevation grid.
        /// </summary>
        [Description("Westernmost coordinate of elevation grid.")]
        [Category("Coordinates")]
        public double WestCoord { get; set; }

        /// <summary>
        /// Easternmost coordinate of elevation grid.
        /// </summary>
        [Description("Easternmost coordinate of elevation grid.")]
        [Category("Coordinates")]
        public double EastCoord { get; set; }

        /// <summary>
        /// Height of elevation grid, in points.
        /// </summary>
        [Description("size of grid.")]
        [Category("Image Size")]
        public int GridSize { get; set; }

        /// <summary>
        /// Planet seed.  Change this to generate a different planet.
        /// </summary>
        [Description("Planet seed.  Change this to generate a different planet.")]
        [Category("Planet Properties")]
        public int Seed { get; set; }

        /// <summary>
        /// Frequency
        /// </summary>
        [Description("Frequency")]
        [Category("Terrain Properties")]
        public double Frequency { get; set; }

        /// <summary>
        /// Lacunarity
        /// </summary>
        [Description("Lacunarity")]
        [Category("Terrain Properties")]
        public double Lacunarity { get; set; }

        /// <summary>
        /// Persistence
        /// </summary>
        [Description("Persistence")]
        [Category("Terrain Properties")]
        public double Persistence { get; set; }

        /// <summary>
        /// Octaves
        /// </summary>
        [Description("Octaves")]
        [Category("Terrain Properties")]
        public int Octaves { get; set; }

        public PlanetGenerationSettings()
        {
            GridSize = 2048;
            Seed = 0;
            Frequency = 0.03;
            Lacunarity = 2.0;
            Persistence = 0.5;
            Octaves = 12;
            NorthCoord = 10;
            WestCoord = -10;
            EastCoord = 10;
            SouthCoord = -10;

        }
    }
}
