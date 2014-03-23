using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace deblur
{
    class PSFEstimator : Computable
    {
        ImgContainer img;
        DirSelector dirSelector;

        private int kernelSize;

        public PSFEstimator(ImgContainer img, DirSelector dirSelector)
        {
            this.img = img;
            this.dirSelector = dirSelector;
            base.addDependency(dirSelector);
        }

        internal override void computeThread()
        {

        }

        public int getKernelSize()
        {
            return kernelSize;
        }

        public void setKernelSize(int kernelSize)
        {
            this.kernelSize = kernelSize;
        }

    }
}
