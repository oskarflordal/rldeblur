using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deblur
{
    class DirSelector : Computable
    {
        float dir;
        Analyze[] kernels;

        public DirSelector(Analyze[] kernels)
        {
            this.kernels = kernels;
            foreach(Computable c  in kernels)
            {
                base.addDependency(c);
            }
        }

        // Rearrange angles so they are easier to use in calculation
        // return the mean of all angles
        private static float rearrAngles(Analyze[] kernels)
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


        // entrypoint
        internal override void computeThread()
        {
            // All right, so att this point we have a bunch of votes for the direction. We could possibly utilize this to find features in the iomage
            // for now we will just sort out the worst offenders and take the mean of the remaning ones.
            float meanVector = rearrAngles(kernels);

            Console.WriteLine("meanVec {0}", meanVector);

            // Sort kernel according to distance from the mean
            Array.Sort(kernels,
                new Comparison<Analyze>((x, y) =>
                {
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
            for (int i = 0; i < kernels.Length / 2; ++i)
            {
                sum += kernels[i].getDir();
            }

            dir = sum / (kernels.Length / 2);
        }

        public float getDir()
        {
            return dir;
        }
    }

}
