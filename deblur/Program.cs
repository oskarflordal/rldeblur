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
            String filename;
            filename = args[0];

            // Load image using first command line arg
            ImgContainer img = new ImgContainer((Bitmap)Bitmap.FromFile(filename));

            try
            {
                // Find the glocal color first
                Average average = new Average(img);
                average.issue();

                // first pick a few point in the image on which to do analysis
                // this will create and issue multiple jobs
                Analyze[] kernels = analyze(img);

                // Take the kernels and estimate the global direction of the blur
                DirSelector dirSelector = new DirSelector(kernels);
                dirSelector.issue();

                // Now lets try to find out the blur kernel size
                PSFEstimator kernelSize = new PSFEstimator(img, dirSelector);
                // well actually lets cheat for now (REVISIT)
                kernelSize.setKernelSize(int.Parse(args[1]));
                kernelSize.issue();

                // The corection kernel utilizing previous information
                RLkernel rl = new RLkernel(img, kernelSize, dirSelector);
                rl.issue();

                // DUmmy wait for RLkernel to finish
                rl.getEvent().WaitOne();
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

        // Launch X new windows where we try to find out the direction and blur kernel
        // launched evenly space according to constants
        private static Analyze[] analyze(ImgContainer img)
        {
            // need to use a large enough image
            // REVISIT
            if (img.width < KERNEL_SIZE * HORIZONTAL_SAMPLE_RES ||
                img.height < KERNEL_SIZE * VERTICAL_SAMPLE_RES)
            {
                throw new NotImplementedException();
            }

            int numKernels = HORIZONTAL_SAMPLE_RES * VERTICAL_SAMPLE_RES;

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
                    kernels[n] = new Analyze(img,
                                             (x + 1) * horiz_dist,
                                             (y + 1) * vert_dist,
                                             KERNEL_SIZE);
                    kernels[n].issue();
                }
            }
            return kernels;

        }


    }
}
