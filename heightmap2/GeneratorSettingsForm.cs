using SharpNoise;
using SharpNoise.Builders;
using SharpNoise.Utilities.Imaging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;
using Color = SharpNoise.Utilities.Imaging.Color;
using SharpNoise.Modules;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ComplexPlanetExample
{
    public partial class GeneratorSettingsForm : Form
    {


        public PlanetGenerationSettings GeneratorSettings { get; set; }

        public GeneratorSettingsForm()
        {
            InitializeComponent();
            GeneratorSettings = new PlanetGenerationSettings();
            propertyGrid.SelectedObject = GeneratorSettings;
        }

        private async void startButton_Click(object sender, EventArgs e)
        {


            var noiseSource = new Perlin
            {
                Seed = GeneratorSettings.Seed,
                Frequency = GeneratorSettings.Frequency,
                Lacunarity = GeneratorSettings.Lacunarity,
                Persistence = GeneratorSettings.Persistence,
                OctaveCount = GeneratorSettings.Octaves
            };

            var noiseMap = new NoiseMap();

            var noiseMapBuilder = new PlaneNoiseMapBuilder
            {
                DestNoiseMap = noiseMap,
                SourceModule = noiseSource
            };
            int quotient = ((int)Math.Round((double)GeneratorSettings.GridSize / 33)) * 33;
            noiseMapBuilder.SetBounds(GeneratorSettings.SouthCoord, GeneratorSettings.NorthCoord,
                GeneratorSettings.WestCoord, GeneratorSettings.EastCoord);
            noiseMapBuilder.SetDestSize(quotient, quotient);

            noiseMapBuilder.Build();

            var degExtent = GeneratorSettings.EastCoord - GeneratorSettings.WestCoord;
            var gridExtent = (double)quotient;
            var planetImage = new SharpNoise.Utilities.Imaging.Image();
            var planetRenderer = new ImageRenderer();
            planetRenderer.SourceNoiseMap = noiseMap;
            planetRenderer.DestinationImage = planetImage;
            planetRenderer.BuildGrayscaleGradient();
            
            planetRenderer.Render();
            planetImage.SaveGdiBitmap("planetgenerated.png", ImageFormat.Png);

            Application.EnableVisualStyles();

            // Create a form
            Form form = new Form();
            form.Text = "PictureBox Example";

            // Create a PictureBox control
            PictureBox pictureBox = new PictureBox
            {
                Width = 1024,
                Height = 1024,
                SizeMode = PictureBoxSizeMode.StretchImage,
                ImageLocation = "planetgenerated.png" // Set the path to your image file
            };

            pictureBox.LoadCompleted += (sender, e) => {
                // Adjust the form size to fit the picture box
                form.ClientSize = new System.Drawing.Size(pictureBox.Width, pictureBox.Height); 
            };

            // Add the PictureBox to the form
            form.Controls.Add(pictureBox);

            // Run the application with the form
            form.ShowDialog();
        }
    }
}
