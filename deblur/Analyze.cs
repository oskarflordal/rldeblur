using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;
using System.Diagnostics;

namespace deblur
{
    class Analyze :  IEquatable<Analyze> , IComparable<Analyze>
    {
        int startX, startY;
        ImgContainer img;

        int kernelSize;

        private float dir;

        // REVISIT C# property?
        public float getDir()
        {
            return dir;
        }

        internal void setDir(float dir)
        {
            this.dir = dir;
        }

        
        private ManualResetEvent done;

        public Analyze(ImgContainer img, int x, int y, ManualResetEvent done, int kernelSize)
        {
            // Init fields
            this.img = img;
            this.startX = x;
            this.startY = y;
            this.done = done;
            this.kernelSize = kernelSize;
        }

        public void compute(Object threadContext)
        {
            findDirection();

            done.Set();
            
        }

        private void findDirection()
        {
            const int STEPS = 31;

            int[][] pelData = new int[kernelSize][];

            //FIXME
            for (int i = 0; i < pelData.Length; ++i)
            {
                pelData[i] = new int[2];
            }


            // Try a range of different directions
            // launching from top<->down to almost down->top in 180 deg arc (others are mirrors)
            // rad = 0 => top to bottom
            float itrStep = (float)Math.PI / (float)(STEPS + 1);

            int maxPos = 0;

            int[] scores = new int[STEPS];

            for (int i = 0; i < STEPS; ++i)
            {
                float curAngle = itrStep * i;
                bresenhamSample(curAngle, kernelSize, startX, startY, pelData);

                // store this score
                scores[i] = frequencyScore(pelData);
            }

            // determine best candidate
            maxPos = analyzeScore(scores);

            dir = maxPos * itrStep;

            // debug
//            Console.WriteLine(
//           "\nDir seems to be {0}", dir);
            Console.WriteLine("ANalyzing {0} {1} => {2}", startX, startY, dir);

            /*
            int[][] markData = new int[11][];
            for (int i = 0; i < markData.Length; ++i)
            {
                markData[i] = new int[2];
            }

            bresenhamSample(maxPos * itrStep, 11, startX, startY, markData);
            img.mark(markData);
             */
        }

        // find the best score candidate by looking at which direction has the largest differance to the direction facing 90 deg 
        private int analyzeScore(int[] scores)
        {
            int maxScore = int.MinValue;
            int maxPos = 0;

            for (int i = 0; i < scores.Length; ++i) {
                int thisScore = scores[(i + scores.Length / 2 + 1) % scores.Length] - scores[i];
                if (thisScore > maxScore)
                {
                    maxScore = thisScore;
                    maxPos = i;
                }
            }
            return maxPos;
        }

        // do a scoring function on the data and score depdning on the amount of high frequency components
        // currently this will sum the diffs between eah set of two pixels
        private int frequencyScore(int[][] pelData)
        {
            // REVISIT inefficient

            int score = 0;

            int[] last = pelData[0];

            foreach (int[] c in pelData)
            {
                score += img.compare(last, c);
                last = c;
            }
            return score;
        }

        // do a bresenham line algo based around a line intersecting startX startY
        // we will walk in both direction until we have kernel_size amount of pixels
        public static void bresenhamSample(float curAngle, int kernelSize, int startX, int startY, int[][] ret)
        {
            Debug.Assert(kernelSize%2 == 1, "Mare sure the kernel size is odd");
            int midSample = kernelSize/2;
            ret[midSample][0] = startX;
            ret[midSample][1] = startY;

            // we iterate from these positions
            // adding mirrored positions
            int posX = 0;
            int posY = 0;

            // determine dx dy, it really onl a relation
            int dx = (int)(10000.0f * Math.Sin(curAngle));
            int dy = (int)(10000.0f * Math.Cos(curAngle));

            int stepX = dx > 0 ? 1 : -1;
            int stepY = dy > 0 ? 1 : -1;

            // abs for dy
            dy = dy * stepY;

            int err = dx - dy;

            // Bresenham iterations
            for (int i = 0; i < kernelSize / 2; ++i)
            {
                int err2 = err * 2;
                if (err2 > -dy)
                {
                    err -= dy;
                    posX += stepX;
                }
                if (err2 < dx)
                {
                    err += dx;
                    posY += stepY;
                }

                // mirrored assign
                ret[midSample + 1 + i][0] = startX + posX;
                ret[midSample + 1 + i][1] = startY + posY;
                ret[midSample - 1 - i][0] = startX - posX;
                ret[midSample - 1 - i][1] = startY - posY;
            }

            // Cool, now we ret should be all filled up!
        }

        // Make sure we can sort
        // Default comparer for Part type. 
        public int CompareTo(Analyze comparePart)
        {
            // A null value means that this object is greater. 
            if (comparePart == null) {
                return 1;
            } else {
                return this.dir.CompareTo(comparePart.dir);
            }
        }

        public override int GetHashCode()
        {
            return startY*img.width + startX;
        }

        public bool Equals(Analyze other)
        {
            if (other == null) return false;
            return (this.dir.Equals(other.dir));
        }


    }
}
