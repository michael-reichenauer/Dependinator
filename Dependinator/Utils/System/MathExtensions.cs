namespace System
{
    public static class MathExtensions
    {
        public static double MM(this double number, double min, double max)
        {
            if (number < min)
            {
                return min;
            }

            if (number > max)
            {
                return max;
            }

            return number;
        }


        public static int MM(this int number, int min, int max)
        {
            if (number < min)
            {
                return min;
            }

            if (number > max)
            {
                return max;
            }

            return number;
        }


        public static float MM(this float number, float min, float max)
        {
            if (number < min)
            {
                return min;
            }

            if (number > max)
            {
                return max;
            }

            return number;
        }
    }
}
