using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace deblur
{
    // Will calculate the averahe intensity values of an image and can launch in a threadpool
    class Average
    {
        ManualResetEvent done;

        ImgContainer img;

        int avgR;
        int avgG;
        int avgB;

        public Average(ManualResetEvent done, ImgContainer img)
        {
            this.done = done;
            this.img = img;
        }

        public void compute(Object threadContext)
        {
            computeAverage();
            done.Set();
        }

        private void computeAverage()
        {

        }

        public void sync()
        {
            done.WaitOne();
        }
    }
}
