namespace Calcpad.Core.Matlab
{
    /// <summary>Mersenne Twister mt19937ar BIT-IDÉNTICO al `rand` de MATLAB ('twister').
    /// Validado contra MATLAB R2017a: rng(1)/rng(42)/rng(0) coinciden a 1e-17.
    /// Reglas de MATLAB descubiertas por RE (ver reference_matlab_rand_mt19937):
    ///   • rand   = genrand_res53 (dos genrand_int32 → double [0,1) de 53 bits).
    ///   • rng(k) para k>0 = init_genrand(k).
    ///   • rng(0)/'default' = init_genrand(5489)  (el seed canónico de mt19937).</summary>
    internal sealed class MatlabTwister
    {
        private const int N = 624, M = 397;
        private const uint MatrixA = 0x9908b0dfu, Upper = 0x80000000u, Lower = 0x7fffffffu;
        private readonly uint[] _mt = new uint[N];
        private int _mti = N + 1;

        public MatlabTwister(uint seed) => Seed(seed);

        /// <summary>Re-siembra como MATLAB: seed 0 → 5489 (default), resto → init_genrand(seed).</summary>
        public void Seed(uint seed) => InitGenrand(seed == 0u ? 5489u : seed);

        private void InitGenrand(uint s)
        {
            _mt[0] = s;
            for (_mti = 1; _mti < N; _mti++)
                _mt[_mti] = (uint)(1812433253u * (_mt[_mti - 1] ^ (_mt[_mti - 1] >> 30)) + (uint)_mti);
        }

        private uint GenrandInt32()
        {
            uint y;
            if (_mti >= N)
            {
                int kk;
                for (kk = 0; kk < N - M; kk++)
                {
                    y = (_mt[kk] & Upper) | (_mt[kk + 1] & Lower);
                    _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ ((y & 1u) != 0 ? MatrixA : 0u);
                }
                for (; kk < N - 1; kk++)
                {
                    y = (_mt[kk] & Upper) | (_mt[kk + 1] & Lower);
                    _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ ((y & 1u) != 0 ? MatrixA : 0u);
                }
                y = (_mt[N - 1] & Upper) | (_mt[0] & Lower);
                _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ ((y & 1u) != 0 ? MatrixA : 0u);
                _mti = 0;
            }
            y = _mt[_mti++];
            y ^= (y >> 11);
            y ^= (y << 7) & 0x9d2c5680u;
            y ^= (y << 15) & 0xefc60000u;
            y ^= (y >> 18);
            return y;
        }

        /// <summary>rand de MATLAB: double [0,1) con 53 bits de resolución (genrand_res53).</summary>
        public double NextDouble()
        {
            uint a = GenrandInt32() >> 5;   // 27 bits
            uint b = GenrandInt32() >> 6;   // 26 bits
            return (a * 67108864.0 + b) / 9007199254740992.0;
        }

        /// <summary>Entero uniforme en [minInclusive, maxExclusive). Para randi (no bit-exacto con
        /// el algoritmo de MATLAB randi, pero uniforme y reproducible sobre el mismo stream).</summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            int range = maxExclusive - minInclusive;
            if (range <= 0) return minInclusive;
            return minInclusive + (int)(NextDouble() * range);
        }
    }
}
