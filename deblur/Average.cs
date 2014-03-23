using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace deblur
{
    // Will calculate the averahe intensity values of an image and can launch in a threadpool
    class Average : Computable
    {
        ImgContainer img;

        int avgR;
        int avgG;
        int avgB;

        public Average(ImgContainer img)
        {
            this.img = img;
        }

        internal override void computeThread()
        {
            // REVISIT
        }
    }
}
