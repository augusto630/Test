namespace Fractal
{
    public class FractalCompute
    {
        public float[] Set { get; set; }

        public int Iterations { get; set; } = 85;

        public float Zoom { get; set; } = 50;

        public float PositionX { get; set; }

        public float PositionY { get; set; }

        public unsafe void ComputeMandelbrot(int viewportWidth, int viewportHeight)
        {
            float localX = this.PositionX;
            float localY = this.PositionY;
            float localZoom = this.Zoom;
            int localMaxIteractions = this.Iterations;

            float zx, zy, cX, cY, x2, y2;
            float lx = localX / viewportWidth;
            float ly = localY / viewportHeight;

            int vw = viewportWidth >> 1;
            int vh = viewportHeight >> 1;

            var set = new float[viewportHeight * viewportWidth];
            int offset = 0;
            fixed (float* setPtr = &set[0])
            {
                for (int y = 0; y < viewportHeight; y++)
                {
                    for (int x = 0; x < viewportWidth; x++)
                    {
                        zx = zy = x2 = y2 = 0;
                        cX = lx + (x - vw) / localZoom;
                        cY = ly + (y - vh) / localZoom;

                        int i = 0;                       
                        while(x2 + y2 <= 4 && i < localMaxIteractions)
                        {
                            zy = 2.0f * zx * zy + cY;
                            zx = x2 - y2 + cX;
                            x2 = zx * zx;
                            y2 = zy * zy;
                            i++;
                        }

                        // If the point is not in the set
                        if (i < localMaxIteractions)
                        {
                            *(setPtr + offset) = (float)i / localMaxIteractions;
                        }

                        offset++;
                    }
                }
            }

            this.Set = set;
        }

        public float PixelValue(int offset)
        {
            if (this.Set == null)
            {
                return 0;
            }

            return this.Set[offset];
        }
    }
}
