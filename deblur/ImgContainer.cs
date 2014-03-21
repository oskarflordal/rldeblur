using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace deblur
{
    class ImgContainer
    {
        // Save the original container around
        Bitmap bitmap;

        public Bitmap getBitmap() {
            return bitmap;
        }

        byte[] buffer;
        public int width;
        public int height;

        int depth;
        BitmapData data;
        private Image image;
        
        public ImgContainer(Bitmap img)
        {
            // Save ref
            bitmap = img;

            // Make the Bitmap into something useable (especially that can handle multiple theads)
            Rectangle rect = new Rectangle(0, 0, img.Width, img.Height);
            data = img.LockBits(rect, ImageLockMode.ReadWrite, img.PixelFormat);
            depth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;

            buffer = new byte[data.Width * data.Height * depth];

            //copy pixels to buffer
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            width = img.Width;
            height = img.Height;
        }

        public int getR(int x, int y)
        {
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= width) x = width - 1;
            if (y >= height) y = height - 1;
            return buffer[(y * width + x) * depth + 2];
        }

        public int getG(int x, int y)
        {
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= width) x = width - 1;
            if (y >= height) y = height - 1;
            return buffer[(y * width + x) * depth + 1];
        }

        public int getB(int x, int y)
        {
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= width) x = width - 1;
            if (y >= height) y = height - 1;
            return buffer[(y * width + x) * depth + 0];
        }

        public void setR(int x, int y, int c)
        {
            buffer[(y * width + x) * depth + 2] = (byte)c;
        }

        public void setG(int x, int y, int c)
        {
            buffer[(y * width + x) * depth + 1] = (byte)c;
        }

        public void setB(int x, int y, int c)
        {
            buffer[(y * width + x) * depth + 0] = (byte)c;
        }


        // reconstruct bitmap
        public void reconstructBitmap() {
            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);

            bitmap.UnlockBits(data);
        }

        // find position in the buffer of a specific pixel
        private int getBytepos(int x, int y)
        {
            return (x + y * width) * depth;
        }

        // comapre all components of two pels
        internal int compare(int[] last, int[] c)
        {
            int diff = 0;

            int bytepos0 = getBytepos(last[0], last[1]);
            int bytepos1 = getBytepos(c[0], c[1]);

            diff += Math.Abs(buffer[bytepos0] - buffer[bytepos1]);
            diff += Math.Abs(buffer[bytepos0+1] - buffer[bytepos1+1]);
            diff += Math.Abs(buffer[bytepos0+2] - buffer[bytepos1+2]);
            return diff;
        }

        // mark a range of pixels with red
        internal void mark(int[][] pelData)
        {
            foreach (int[] c in pelData)
            {
                buffer[getBytepos(c[0], c[1]) + 2] = 0xff;
            }
        }
    }
}
