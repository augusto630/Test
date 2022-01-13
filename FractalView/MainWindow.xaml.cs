namespace FractalView
{
    using Fractal;

    using FractalView;

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Coordinates
        private float xPos;
        private float yPos;

        // Number of max iterations
        private int maxIter = 20;
        // Zoom factor
        private float zoom = 150;

        private int fractalWidth = 1024;
        private int fractalHeight = 1024;

        // Algorithm variables
        private float zx, zy, cX, cY, tmp;

        private WriteableBitmap bmp;
        private bool running;

        private object lockObject = new object();
        private object debounceControl;

        private int processTime = 200;
        private float speed = 30000;

        public MainWindow()
        {
            this.InitializeComponent();

            xPos = (float)(this.Width / 2.0f);
            yPos = (float)(this.Height / 2.0f);

            this.ComputeFractalAsync();

            this.SizeChanged += (sender, ea) => this.ComputeFractalAsync(); ;
            this.KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                    xPos -= speed / zoom;
                    break;
                case Key.Right:
                    xPos += speed / zoom;
                    break;
                case Key.Up:
                    yPos -= speed / zoom;
                    break;
                case Key.Down:
                    yPos += speed / zoom;
                    break;
                case Key.Add:
                    zoom *= 1.1f;
                    break;
                case Key.Subtract:
                    zoom *= 0.9f;
                    break;

                default:
                    return;
            }

            this.ComputeFractalAsync();
        }

        private unsafe void RenderFractal()
        {
            lock (this.lockObject)
            {
                if (this.running)
                {
                    return;
                }

                this.running = true;
                Application.Current.Dispatcher.Invoke(() => this.Title = "Running...");
            }

            var stp = Stopwatch.StartNew();

            int imageWidth = fractalWidth;
            int imageHeight = fractalHeight;

            int windowWidth = 500;
            int windowHeight = 500;

            Application.Current.Dispatcher.Invoke(() =>
            {
                windowWidth = (int)this.Width;
                windowHeight = (int)this.Height;
            });

            var bmp = new WriteableBitmap(imageWidth, imageHeight, 96, 96, PixelFormats.Gray16, null);
            bmp.Lock();

            unsafe
            {
                // Get a pointer to the back buffer.
                ushort* pBackBuffer = (ushort*)bmp.BackBuffer.ToPointer();
                ushort colorData = 0;
                float r = 0;
                float g = 0;
                float b = 0;

                for (int y = 0; y < imageHeight; y++)
                {
                    for (int x = 0; x < imageWidth; x++)
                    {
                        zx = zy = 0;
                        cX = xPos / windowWidth + (x - (imageWidth >> 1)) / zoom;
                        cY = yPos / windowHeight + (y - (imageHeight >> 1)) / zoom;
                        ////cX = (x - xPos) / zoom;
                        ////cY = (y - yPos) / zoom;

                        int iter = 0;

                        ////Parallel.For(0, maxIter, (i, state) =>
                        ////{
                        ////    System.Threading.Interlocked.Increment(ref iter);
                        ////    tmp = zx * zx - zy * zy + cX;
                        ////    zy = 2.0 * zx * zy + cY;
                        ////    zx = tmp;

                        ////    if (zx * zx + zy * zy >= 4)
                        ////    {
                        ////        state.Break();
                        ////    }
                        ////});

                        for (iter = 0; iter < maxIter && zx * zx + zy * zy < 4; iter++)
                        {
                            tmp = zx * zx - zy * zy + cX;
                            zy = 2.0f * zx * zy + cY;
                            zx = tmp;

                            if (float.IsInfinity(zx * zx + zy * zy))
                            {
                                break;
                            }

                        }

                        // If the point is in the set
                        if (iter == maxIter)
                        {
                            colorData = 0;
                        }
                        // If the point is not in the set
                        else
                        {
                            r = iter | (iter << 2);
                            while (r > 255) { r -= 255; }
                            g = iter | (iter << 4);
                            while (g > 255) { g -= 255; }
                            b = iter | (iter << 8);
                            while (b > 255) { b -= 255; }

                            var intensity = ((r + g + b) / 3.0) / 255.0;
                            if (intensity > 1)
                            {
                                intensity = 1;
                            }

                            if (intensity < 0)
                            {
                                intensity = 0;
                            }

                            colorData = (ushort)(intensity * ushort.MaxValue);
                        }

                        // Compute the pixel's color.
                        ////colorData = (int)r << 16; // R
                        ////colorData |= (int)g << 8;   // G
                        ////colorData |= (int)b << 0;   // B

                        // Assign the color data to the pixel.
                        // Find the address of the pixel to draw.
                        int offset = y * bmp.BackBufferStride / sizeof(ushort) + x;
                        *(pBackBuffer + offset) = colorData;
                    }
                }
            }

            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();

            bmp.Freeze();

            Application.Current.Dispatcher.Invoke(() => this.Image.Source = bmp);

            Application.Current.Dispatcher.Invoke(() => this.Title = stp.ElapsedMilliseconds.ToString());

            stp.Stop();
            this.processTime = (int)stp.ElapsedMilliseconds;
            this.running = false;
        }

        private unsafe void RenderFractal2()
        {
            lock (this.lockObject)
            {
                if (this.running)
                {
                    return;
                }

                this.running = true;
                Application.Current.Dispatcher.Invoke(() => this.Title = $"[{this.xPos};{this.yPos};{this.zoom}] Running...");
            }

            var stp = Stopwatch.StartNew();

            int windowWidth = 0;
            int windowHeight = 0;

            Application.Current.Dispatcher.Invoke(() =>
            {
                windowWidth = (int)Math.Max(this.ActualWidth, this.Width);
                windowHeight = (int)Math.Max(this.ActualHeight, this.Height);
            });

            var bmp = new WriteableBitmap(windowWidth, windowHeight, 96, 96, PixelFormats.Gray16, null);
            bmp.Lock();

            var compute = new FractalCompute();
            compute.PositionX = this.xPos;
            compute.PositionY = this.yPos;
            compute.Zoom = this.zoom;
            compute.ComputeMandelbrot(windowWidth, windowHeight);

            // Get a pointer to the back buffer.
            ushort* pBackBuffer = (ushort*)bmp.BackBuffer.ToPointer();

            for (int i = 0; i < windowWidth * windowHeight; i++)
            {
                *pBackBuffer = (ushort)(compute.PixelValue(i) * ushort.MaxValue);
                pBackBuffer++;
            }

            bmp.AddDirtyRect(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
            bmp.Unlock();

            bmp.Freeze();

            Application.Current.Dispatcher.Invoke(() =>
            {
                this.Title = $"[{this.xPos};{this.yPos};{this.zoom}] {stp.ElapsedMilliseconds}ms";
                this.Image.Source = bmp;
            });

            stp.Stop();
            this.processTime = (int)stp.ElapsedMilliseconds;
            this.running = false;
        }

        private Task ComputeFractalAsync()
        {
            return ((Action)RenderFractal2).FastThrottle(ref this.debounceControl, delay_ms: 0);
        }
    }
}
