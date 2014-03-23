using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deblur
{

    // Richardson lucy kernel
    class RLkernel : Computable
    {
        ImgContainer img;
        int kernelSize;
        PSFEstimator psf;

        DirSelector dirSelector;

        internal override void computeThread()
        {
            kernelSize = psf.getKernelSize();
            correct(dirSelector.getDir());
        }

        public RLkernel(ImgContainer img, PSFEstimator psf, DirSelector dirSelector)
            : base()
        {
            this.img = img;
            this.psf = psf;
            this.dirSelector = dirSelector;

            // We need to have the direction ready
            base.addDependency(dirSelector);
            base.addDependency(psf);
        }

        private void correct(float dir)
        {
            // run Richardson-Lucy deconvolution using these settings

            // large buffer for intermediate calculation
            double[] ubuf = new double[img.width * img.height * 4];
            double[] ubufNew = new double[img.width * img.height * 4];
            double[] dbuf = new double[img.width * img.height * 4];
            double[] ubufTmp;

            // assuming a straight line for the spread function
            int[][] pelCoords = new int[kernelSize][];
            for (int i = 0; i < pelCoords.Length; ++i)
            {
                pelCoords[i] = new int[2];
            }
            Analyze.bresenhamSample(dir, kernelSize, 0, 0, pelCoords);

            initDbuf(img, dbuf, ubuf, pelCoords, kernelSize);

            // iterate to a better image (hopefully)
            for (int rlLoop = 0; rlLoop < 5; ++rlLoop)
            {

                updateU(kernelSize / 2, img.width - kernelSize / 2 - 1,
                        kernelSize / 2, img.height - kernelSize / 2 - 1,
                        img, ubuf, ubufNew, dbuf, pelCoords, kernelSize);

                // swap the buffer pointers
                ubufTmp = ubuf;
                ubuf = ubufNew;
                ubufNew = ubufTmp;
            }

            // Set the final ubuf to the bitmap
            writeUbufToImg(ubuf, img);
        }

        // data conversion from the accumulating doubles to bytes
        private static void writeUbufToImg(double[] ubuf, ImgContainer img)
        {
            for (int y = 0; y < img.height; ++y)
            {
                for (int x = 0; x < img.width; ++x)
                {
                    img.setR(x, y, (int)ubuf[4 * (y * img.width + x) + 2]);
                    img.setG(x, y, (int)ubuf[4 * (y * img.width + x) + 1]);
                    img.setB(x, y, (int)ubuf[4 * (y * img.width + x) + 0]);
                }
            }
        }

        private static void initDbuf(ImgContainer img, double[] dbuf, double[] ubuf, int[][] pelCoords, int kernelSize)
        {
            // collect the observed value sum for each pixel
            // this will be the same for all iterations
            for (int y = 0; y < img.height; ++y)
            {
                for (int x = 0; x < img.width; ++x)
                {
                    int thisAddr = 4 * (y * img.width + x);
                    dbuf[thisAddr + 2] = 0;
                    dbuf[thisAddr + 1] = 0;
                    dbuf[thisAddr + 0] = 0;

                    for (int i = 0; i < kernelSize; ++i)
                    {
                        int thisX = x + pelCoords[i][0];
                        int thisY = y + pelCoords[i][1];
                        dbuf[thisAddr + 2] += img.getR(thisX, thisY);
                        dbuf[thisAddr + 1] += img.getG(thisX, thisY);
                        dbuf[thisAddr + 0] += img.getB(thisX, thisY);
                    }

                    ubuf[thisAddr + 0] = 128.0;
                    ubuf[thisAddr + 1] = 128.0;
                    ubuf[thisAddr + 2] = 128.0;
                }
            }
        }

        private static void updateU(int x0, int x1, int y0, int y1, ImgContainer img, double[] ubuf, double[] ubufNew, double[] dbuf, int[][] pelCoords, int kernelSize)
        {
            // Lazyly parallelizing y, seems like letting c# decide works best
            const int ROWS_PER_THREAD = 1;
            Parallel.For(0, (y1 - y0 + ROWS_PER_THREAD - 1) / ROWS_PER_THREAD, yhup =>
            {
                for (int y = y0 + yhup * ROWS_PER_THREAD; y < y0 + (yhup + 1) * ROWS_PER_THREAD && y < y1; ++y)
                {

                    for (int x = x0; x < x1; ++x)
                    {
                        double uSumR = 0;
                        double uSumG = 0;
                        double uSumB = 0;
                        for (int i = 0; i < kernelSize; ++i)
                        {
                            int thisX = x + pelCoords[i][0];
                            int thisY = y + pelCoords[i][1];
                            int thisAddr = 4 * (thisY * img.width + thisX);
                            uSumR += ubuf[thisAddr + 2];
                            uSumG += ubuf[thisAddr + 1];
                            uSumB += ubuf[thisAddr + 0];
                        }

                        int dAddr = 4 * (y * img.width + x);

                        double dR = dbuf[dAddr + 2];
                        double dG = dbuf[dAddr + 1];
                        double dB = dbuf[dAddr + 0];

                        ubufNew[dAddr + 2] = ubuf[dAddr + 2] * dR / uSumR;
                        ubufNew[dAddr + 1] = ubuf[dAddr + 1] * dG / uSumG;
                        ubufNew[dAddr + 0] = ubuf[dAddr + 0] * dB / uSumB;
                    }
                }
            });
        }
    }
}
