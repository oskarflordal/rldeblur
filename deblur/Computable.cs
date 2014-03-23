using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace deblur
{
    abstract class Computable {

        LinkedList<ManualResetEvent> dependencies;

        // indicate that we are done;
        ManualResetEvent done;

        public Computable()
        {
            done = new ManualResetEvent(false);
            dependencies = new LinkedList<ManualResetEvent>();
        }

        public void addDependency(Computable cmp)
        {
            dependencies.AddLast(cmp.getEvent());
        }

        public void compute(Object threadContext)
        {
            computeNonDependent();
            
            // wait through each dependency
            // assuming they are never being undone once done
            foreach (ManualResetEvent e in dependencies)
            {
                e.WaitOne();
            }

            computeThread();

            done.Set();
        }

        public void issue()
        {
            ThreadPool.QueueUserWorkItem(compute);
        }

        // Implement this in inheritic class
        internal abstract void computeThread();

        private void computeNonDependent()
        {

        }


        public ManualResetEvent getEvent()
        {
            return done;
        }
    }
}
