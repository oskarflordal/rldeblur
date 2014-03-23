using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;


/* TODO
 * It would probably make sense to go Y only first
 */

namespace deblur
{
    class Program
    {
        public const int VERTICAL_SAMPLE_RES = 9;
        public const int HORIZONTAL_SAMPLE_RES = 5;
        // make sure its odd
        public const int KERNEL_SIZE = 31;

        public const bool MULTI_THREADED = true;

        static void Main(string[] args)
        {
            WorkDispatch workD = new WorkDispatch();

            String filename;
            filename = args[0];

            // Load image using first command line arg
            ImgContainer img = new ImgContainer((Bitmap)Bitmap.FromFile(filename));

            try
            {
                // Find the glocal color first
                determineAverage();

                // Algo overview
                // first pick a few point in the image on which to do analysis
                float dir = analyze(img);

#if DEBUG
                Console.WriteLine("We have decided the direction is {0}", dir);
#endif

                // Now lets try to find out the blur kernel size
                int kernelSize = findBlurSize();
                // well actually lets cheat for now (REVISIT)
                kernelSize = int.Parse(args[1]);

                correct(img, kernelSize, dir);
            }
            catch (Exception e)
            {
                // something was NYI
                Console.WriteLine(
                  "\nStackTrace ---\n{0}", e.StackTrace);                
            }


            // We are done, save the result
            img.reconstructBitmap();
            img.getBitmap().Save(filename + ".png", ImageFormat.Png);
        }

        private static void debugMarkVectors(ImgContainer img, float dir)
        {
            int[][] markData = new int[11][];
            for (int i = 0; i < markData.Length; ++i)
            {
                markData[i] = new int[2];
            }

            Analyze.bresenhamSample(dir, 21, img.width/2, img.height/2, markData);
            img.mark(markData);
        }

        private static int findBlurSize()
        {
            // NIY
            return 11;
        }

        // Launch X new windows where we try to find out the direction and blur kernel
        // launched evenly space according to constants
        private static float analyze(ImgContainer img)
        {
            // need to use a large enough image
            // REVISIT
            if (img.width < KERNEL_SIZE * HORIZONTAL_SAMPLE_RES ||
                img.height < KERNEL_SIZE * VERTICAL_SAMPLE_RES)
            {
                throw new NotImplementedException();
            }

            int numKernels = HORIZONTAL_SAMPLE_RES * VERTICAL_SAMPLE_RES;

            ManualResetEvent[] dones = new ManualResetEvent[numKernels];
            Analyze[] kernels = new Analyze[numKernels];

            // distance between samples
            int vert_dist = img.height / (VERTICAL_SAMPLE_RES + 1);
            int horiz_dist = img.width / (HORIZONTAL_SAMPLE_RES + 1);

            for (int y = 0; y < VERTICAL_SAMPLE_RES; ++y)
            {
                for (int x = 0; x < HORIZONTAL_SAMPLE_RES; ++x)
                {
                    // launch analyze object for this point to the ThreadPool
                    int n = y * HORIZONTAL_SAMPLE_RES + x;
                    dones[n] = new ManualResetEvent(false);
                    kernels[n] = new Analyze(img,
                                             (x + 1) * horiz_dist,
                                             (y + 1) * vert_dist,
                                             dones[n],
                                             KERNEL_SIZE);
                    if (MULTI_THREADED)
                    {
                        ThreadPool.QueueUserWorkItem(kernels[n].compute);
                    }
                    else
                    {
                        kernels[n].findDirection();
                    }
                }
            }

            // Wait until all Threads are done before moving on
            if (MULTI_THREADED)
            {
                WaitHandle.WaitAll(dones);
            }

            // All right, so att this point we have a bunch of votes for the direction. We could possibly utilize this to find features in the iomage
            // for now we will just sort out the worst offenders and take the mean of the remaning ones.
            float meanVector = rearrAngles(kernels);

            Console.WriteLine("meanVec {0}", meanVector);                

            // Sort kernel according to distance from the mean
            Array.Sort(kernels,  
                new Comparison<Analyze> ((x, y) => {
                    return Math.Abs(x.getDir() - meanVector).CompareTo(Math.Abs(y.getDir() - meanVector));
                })
            );

#if DEBUG
            foreach (Analyze a in kernels)
            {
                Console.WriteLine("sorted {0}", a.getDir());                
            }
#endif

            float sum = 0;

            // average the first half of the vectors
            for (int i = 0; i < numKernels / 2; ++i)
            {
                sum += kernels[i].getDir();
            }

            return sum/(numKernels/2);
        }

        // Rearrange angles so they are easier to use in calculation
        // return the mean of all angles
        private static float rearrAngles(Analyze [] kernels)
        {
            // first grab the "mean" vector by summing up the unity vectors
            double sumX = 0;
            double sumY = 0;

            // Things are easier if we find out the general direction of the inclinations are they up<->down or left<->right
            int leftright = 0;
            int updown = 0;
            foreach (Analyze a in kernels)
            {
                if (Math.Sin(a.getDir()) > Math.Cos(a.getDir()))
                {
                    leftright++;
                }
                else
                {
                    updown++;
                }
            }

            Console.WriteLine("updown {0} {1}", updown, leftright);                


            // if we are updown bound we want to make sure the angles are in the range -Pi/2 to PI/2
            foreach (Analyze a in kernels)
            {
                float dir = a.getDir();
                if (updown > leftright && dir > Math.PI / 2)
                {
                    dir -= (float)Math.PI;
                    a.setDir(dir);
                }
                sumX += Math.Sin(dir);
                sumY += Math.Cos(dir);
            }

            float meanVec = (float)Math.Atan(sumX / sumY);

            return meanVec < 0.0f ? -meanVec : meanVec;
        }

        // This may trash the cache while running together with the other threads
        private static void determineAverage()
        {
//            throw new NotImplementedException();
        }

        private static void correct(ImgContainer img, int kernelSize, float dir)
        {
            // run Richardson-Lucy deconvolution using these settings

            // large buffer for intermediate calculation
            double[] ubuf = new double[img.width * img.height * 4];
            double[] ubufNew = new double[img.width * img.height * 4];
            double[] dbuf = new double[img.width * img.height * 4];
            double[] ubufTmp;

            // assuming a straight line for the spread function
            // not really necessary as long as we assume all samples are equal
//            float psf = 1 / kernelSize;

            // psf line
            int[][] pelCoords = new int[kernelSize][];
            for (int i = 0; i < pelCoords.Length; ++i)
            {
                pelCoords[i] = new int[2];
            }

            Analyze.bresenhamSample(dir, kernelSize, 0, 0, pelCoords);

            // collect the observed value sum for each pixel
            for (int y = 0; y < img.height; ++y) {
                for (int x = 0; x < img.width;  ++x)
                {
                    int thisAddr = 4 * (y * img.width + x);
                    dbuf[thisAddr+2] = 0;
                    dbuf[thisAddr+1] = 0;
                    dbuf[thisAddr+0] = 0;

                    for (int i = 0; i < kernelSize; ++i)
                    {
                        int thisX = x + pelCoords[i][0];
                        int thisY = y + pelCoords[i][1];
                        dbuf[thisAddr + 2] += img.getR(thisX, thisY);
                        dbuf[thisAddr + 1] += img.getG(thisX, thisY);
                        dbuf[thisAddr + 0] += img.getB(thisX, thisY);
                    }

//                    Console.WriteLine("updown {0}", dbuf[thisAddr + 2]);                

                    ubuf[thisAddr + 0] = 128.0;
                    ubuf[thisAddr + 1] = 128.0;
                    ubuf[thisAddr + 2] = 128.0;
                }
            }

            // iterate to a better image (hopefully)
            for (int rlLoop = 0; rlLoop < 10; ++rlLoop)
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

        private static void updateU(int x0, int x1, int y0, int y1, ImgContainer img, double[] ubuf, double[] ubufNew, double[] dbuf, int[][] pelCoords, int kernelSize)
        {
            // Lazyly parallelizing y, seems like letting c# decide works best
            const int ROWS_PER_THREAD = 1;
            Parallel.For (0, (y1-y0 + ROWS_PER_THREAD-1)/ROWS_PER_THREAD, yhup => 
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
