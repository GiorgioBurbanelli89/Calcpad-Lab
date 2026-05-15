// =============================================================================
// Calcpad Lab — MATLAB Control System Toolbox (MVP)
// =============================================================================
//   Modelos de sistemas lineales: tf(num, den), zpk(z, p, k).
//   Respuestas: step, impulse, lsim.
//   Frecuencia: bode, nyquist, margin.
//   Operaciones: series, parallel, feedback.
//   Análisis: pole, zero, damp, dcgain.
//
//   Modelo interno: TF (transfer function) = num polinómico / den polinómico
//   almacenado como struct MATLAB con campos { num, den, type='tf' }.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Calcpad.Core.Matlab
{
    internal static class MatlabControl
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Crea un struct MATLAB con la representación TF.</summary>
        public static MValue Tf(double[] num, double[] den)
        {
            var st = MValue.NewStruct();
            st.Fields["num"] = new MValue(1, num.Length, (double[])num.Clone());
            st.Fields["den"] = new MValue(1, den.Length, (double[])den.Clone());
            st.Fields["type"] = new MValue("tf");
            return st;
        }
        public static MValue Zpk(double[] zeros, double[] poles, double gain)
        {
            // Convertir a num/den
            var num = PolyFromRoots(zeros);
            var den = PolyFromRoots(poles);
            for (int i = 0; i < num.Length; i++) num[i] *= gain;
            var st = Tf(num, den);
            st.Fields["zeros"] = new MValue(zeros.Length, 1, (double[])zeros.Clone());
            st.Fields["poles"] = new MValue(poles.Length, 1, (double[])poles.Clone());
            st.Fields["gain"] = new MValue(gain);
            st.Fields["type"] = new MValue("zpk");
            return st;
        }
        private static double[] PolyFromRoots(double[] roots)
        {
            // Polinomio mónico con dichas raíces
            var p = new double[] { 1 };
            foreach (var r in roots)
            {
                var newP = new double[p.Length + 1];
                for (int i = 0; i < p.Length; i++) { newP[i] += p[i]; newP[i + 1] -= r * p[i]; }
                p = newP;
            }
            return p;
        }

        /// <summary>Evalúa H(s) en un complejo s = re + im·i.</summary>
        public static (double re, double im) Evaluate(double[] num, double[] den, double sRe, double sIm)
        {
            double nRe = 0, nIm = 0;
            for (int i = 0; i < num.Length; i++)
            {
                int p = num.Length - 1 - i;
                var (re, im) = ComplexPow(sRe, sIm, p);
                nRe += num[i] * re; nIm += num[i] * im;
            }
            double dRe = 0, dIm = 0;
            for (int i = 0; i < den.Length; i++)
            {
                int p = den.Length - 1 - i;
                var (re, im) = ComplexPow(sRe, sIm, p);
                dRe += den[i] * re; dIm += den[i] * im;
            }
            double denom = dRe * dRe + dIm * dIm;
            if (denom < 1e-30) return (double.PositiveInfinity, 0);
            return ((nRe * dRe + nIm * dIm) / denom, (nIm * dRe - nRe * dIm) / denom);
        }
        private static (double, double) ComplexPow(double re, double im, int p)
        {
            if (p == 0) return (1, 0);
            double r = 1, i = 0;
            for (int k = 0; k < p; k++)
            {
                double nr = r * re - i * im;
                double ni = r * im + i * re;
                r = nr; i = ni;
            }
            return (r, i);
        }

        /// <summary>Step response — integra el sistema con entrada escalón unitario.</summary>
        public static (MValue t, MValue y) StepResponse(double[] num, double[] den, double tFinal)
        {
            // Convertir a state-space (forma canónica controlable) y simular con ode45
            // dx/dt = A·x + B·u, y = C·x + D·u, u(t) = 1
            int n = den.Length - 1;
            if (n < 1) throw new MatlabRuntimeException("step: orden 0");
            // Normalizar den para a_n = 1
            double a_n = den[0];
            var aNorm = new double[n + 1];
            for (int i = 0; i <= n; i++) aNorm[i] = den[i] / a_n;
            var bNorm = new double[num.Length];
            for (int i = 0; i < num.Length; i++) bNorm[i] = num[i] / a_n;
            // Companion matrix A
            var A = new double[n, n];
            for (int i = 0; i < n - 1; i++) A[i, i + 1] = 1;
            for (int j = 0; j < n; j++) A[n - 1, j] = -aNorm[n - j];
            // B = [0; 0; ...; 1]
            var B = new double[n]; B[n - 1] = 1;
            // C: extender bNorm a longitud n+1 (padding leading zeros)
            var bFull = new double[n + 1];
            int offset = n + 1 - bNorm.Length;
            for (int i = 0; i < bNorm.Length; i++) bFull[i + offset] = bNorm[i];
            var C = new double[n];
            double D = bFull[0];
            for (int j = 0; j < n; j++) C[j] = bFull[n - j] - D * aNorm[n - j];
            // Simular: ode45 RK4 con h fijo
            int Nt = 500;
            double h = tFinal / Nt;
            var ts = new MValue(Nt + 1, 1);
            var ys = new MValue(Nt + 1, 1);
            var x = new double[n];
            for (int k = 0; k <= Nt; k++)
            {
                double t = k * h;
                ts.Set(k, 0, t);
                double y_t = D;
                for (int j = 0; j < n; j++) y_t += C[j] * x[j];
                ys.Set(k, 0, y_t);
                if (k == Nt) break;
                // RK4
                var k1 = Deriv(A, B, x, 1.0);
                var x2 = AddScale(x, k1, h / 2);
                var k2 = Deriv(A, B, x2, 1.0);
                var x3 = AddScale(x, k2, h / 2);
                var k3 = Deriv(A, B, x3, 1.0);
                var x4 = AddScale(x, k3, h);
                var k4 = Deriv(A, B, x4, 1.0);
                for (int j = 0; j < n; j++)
                    x[j] += h * (k1[j] + 2 * k2[j] + 2 * k3[j] + k4[j]) / 6;
            }
            return (ts, ys);
        }
        public static (MValue t, MValue y) ImpulseResponse(double[] num, double[] den, double tFinal)
        {
            // Impulse = d/dt(step) — usamos derivada numérica de la step response
            var (ts, ys) = StepResponse(num, den, tFinal);
            int N = ys.Data.Length;
            var impy = new MValue(N, 1);
            for (int i = 0; i < N - 1; i++)
                impy.Set(i, 0, (ys.At(i + 1, 0) - ys.At(i, 0)) / (ts.At(i + 1, 0) - ts.At(i, 0)));
            impy.Set(N - 1, 0, impy.At(N - 2, 0));
            return (ts, impy);
        }
        private static double[] Deriv(double[,] A, double[] B, double[] x, double u)
        {
            int n = x.Length;
            var dx = new double[n];
            for (int i = 0; i < n; i++)
            {
                double s = B[i] * u;
                for (int j = 0; j < n; j++) s += A[i, j] * x[j];
                dx[i] = s;
            }
            return dx;
        }
        private static double[] AddScale(double[] a, double[] b, double s)
        {
            var r = new double[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = a[i] + s * b[i];
            return r;
        }

        /// <summary>Bode magnitude (dB) + phase (degrees) over ω range.</summary>
        public static (double[] w, double[] magDb, double[] phaseDeg) Bode(double[] num, double[] den, double wMin, double wMax, int N)
        {
            var w = new double[N];
            var mag = new double[N];
            var ph = new double[N];
            for (int k = 0; k < N; k++)
            {
                double logW = Math.Log10(wMin) + k * (Math.Log10(wMax) - Math.Log10(wMin)) / (N - 1);
                w[k] = Math.Pow(10, logW);
                var (re, im) = Evaluate(num, den, 0, w[k]);
                double m = Math.Sqrt(re * re + im * im);
                mag[k] = 20 * Math.Log10(Math.Max(m, 1e-30));
                ph[k] = Math.Atan2(im, re) * 180 / Math.PI;
            }
            // Unwrap fase
            for (int k = 1; k < N; k++)
            {
                while (ph[k] - ph[k - 1] > 180) ph[k] -= 360;
                while (ph[k] - ph[k - 1] < -180) ph[k] += 360;
            }
            return (w, mag, ph);
        }
        /// <summary>Nyquist (polos del sistema closed-loop) — devuelve re/im de H(jω) sobre ω range.</summary>
        public static (double[] re, double[] im) Nyquist(double[] num, double[] den, double wMin, double wMax, int N)
        {
            var re = new double[N];
            var im = new double[N];
            for (int k = 0; k < N; k++)
            {
                double logW = Math.Log10(wMin) + k * (Math.Log10(wMax) - Math.Log10(wMin)) / (N - 1);
                double w = Math.Pow(10, logW);
                var (r, i) = Evaluate(num, den, 0, w);
                re[k] = r; im[k] = i;
            }
            return (re, im);
        }

        /// <summary>
        /// Resuelve la Ecuación Algebraica de Riccati Continua:
        ///   A'P + PA - PBR⁻¹B'P + Q = 0
        /// vía iteración Newton-Kleinman: P_{k+1} desde P_k.
        /// </summary>
        public static MValue Care(MValue A, MValue B, MValue Q, MValue R)
        {
            int n = A.Rows;
            // Inicialización P = Q (asegura definida positiva)
            var P = new double[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) P[i, j] = Q.At(i, j);
            // Iteración Newton-Kleinman (~50 iter)
            for (int iter = 0; iter < 100; iter++)
            {
                // K = R⁻¹ B' P
                int m = B.Cols;
                var BtP = new double[m, n];
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < n; j++)
                    {
                        double s = 0;
                        for (int k = 0; k < n; k++) s += B.At(k, i) * P[k, j];
                        BtP[i, j] = s;
                    }
                // R K = B' P → resolver. Asumimos R diagonal/escalar para MVP
                var K = new double[m, n];
                // Resolver R·K = BtP por LU (Linsolve column-wise)
                for (int col = 0; col < n; col++)
                {
                    var rhs = new MValue(m, 1);
                    for (int i = 0; i < m; i++) rhs.Set(i, 0, BtP[i, col]);
                    var Kcol = MatlabLinAlg.Linsolve(R, rhs);
                    for (int i = 0; i < m; i++) K[i, col] = Kcol.At(i, 0);
                }
                // Acl = A - B·K
                var Acl = new double[n, n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        double s = A.At(i, j);
                        for (int k = 0; k < m; k++) s -= B.At(i, k) * K[k, j];
                        Acl[i, j] = s;
                    }
                // Lyapunov: A_cl' P + P A_cl = -Q - K'·R·K
                // Resolver vía vec(P) = (I⊗Acl' + Acl'⊗I)⁻¹ vec(-Q-K'RK)
                // MVP: resolver via Bartels-Stewart simple (asume Acl estable)
                var Qaug = new double[n, n];
                for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) Qaug[i, j] = Q.At(i, j);
                // + K' R K
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        double s = 0;
                        for (int u = 0; u < m; u++)
                            for (int v = 0; v < m; v++)
                                s += K[u, i] * R.At(u, v) * K[v, j];
                        Qaug[i, j] += s;
                    }
                // Bartels-Stewart simplificado: vectorize y resolver
                var bigA = new MValue(n * n, n * n);
                var bigB = new MValue(n * n, 1);
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        int row = i * n + j;
                        // Acl'·P + P·Acl en posición (i,j)
                        for (int k = 0; k < n; k++)
                        {
                            bigA.Set(row, k * n + j, bigA.At(row, k * n + j) + Acl[k, i]);
                            bigA.Set(row, i * n + k, bigA.At(row, i * n + k) + Acl[k, j]);
                        }
                        bigB.Set(row, 0, -Qaug[i, j]);
                    }
                MValue pVec;
                try { pVec = MatlabLinAlg.Linsolve(bigA, bigB); }
                catch { break; }
                var Pnew = new double[n, n];
                double diff = 0;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        Pnew[i, j] = pVec.At(i * n + j, 0);
                        diff += (Pnew[i, j] - P[i, j]) * (Pnew[i, j] - P[i, j]);
                    }
                P = Pnew;
                if (Math.Sqrt(diff) < 1e-12) break;
            }
            var result = new MValue(n, n);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) result.Set(i, j, P[i, j]);
            return result;
        }
        /// <summary>LQR — retorna K (ganancia óptima estado-feedback).</summary>
        public static (MValue K, MValue P, MValue eigVals) Lqr(MValue A, MValue B, MValue Q, MValue R)
        {
            var P = Care(A, B, Q, R);
            int n = A.Rows, m = B.Cols;
            // K = R⁻¹ B' P
            var BtP = new MValue(m, n);
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                {
                    double s = 0;
                    for (int k = 0; k < n; k++) s += B.At(k, i) * P.At(k, j);
                    BtP.Set(i, j, s);
                }
            // Resolver R·K = B'P
            var K = new MValue(m, n);
            for (int col = 0; col < n; col++)
            {
                var rhs = new MValue(m, 1);
                for (int i = 0; i < m; i++) rhs.Set(i, 0, BtP.At(i, col));
                var Kcol = MatlabLinAlg.Linsolve(R, rhs);
                for (int i = 0; i < m; i++) K.Set(i, col, Kcol.At(i, 0));
            }
            // Eigenvalores de A - B·K
            var Acl = new MValue(n, n);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    double s = A.At(i, j);
                    for (int k = 0; k < m; k++) s -= B.At(i, k) * K.At(k, j);
                    Acl.Set(i, j, s);
                }
            return (K, P, MatlabLinAlg.Eig(Acl).eigenvalues);
        }

        /// <summary>Step info — RiseTime, Overshoot, SettlingTime, Peak.</summary>
        public static MValue StepInfo(double[] t, double[] y, double yFinal)
        {
            int n = y.Length;
            // Peak y peak time
            double peak = double.NegativeInfinity; int peakIdx = 0;
            for (int i = 0; i < n; i++) if (y[i] > peak) { peak = y[i]; peakIdx = i; }
            // Overshoot %
            double overshoot = yFinal != 0 ? (peak - yFinal) / Math.Abs(yFinal) * 100 : 0;
            if (overshoot < 0) overshoot = 0;
            // RiseTime: 10% → 90% (time)
            double t10 = -1, t90 = -1;
            for (int i = 0; i < n; i++)
            {
                if (t10 < 0 && y[i] >= 0.1 * yFinal) t10 = t[i];
                if (t90 < 0 && y[i] >= 0.9 * yFinal) t90 = t[i];
            }
            double riseTime = (t10 >= 0 && t90 >= 0) ? t90 - t10 : double.NaN;
            // SettlingTime (2% band)
            double tSettle = t[n - 1];
            for (int i = n - 1; i >= 0; i--)
                if (Math.Abs(y[i] - yFinal) > 0.02 * Math.Abs(yFinal)) { tSettle = t[Math.Min(i + 1, n - 1)]; break; }
            var info = MValue.NewStruct();
            info.Fields["RiseTime"] = new MValue(riseTime);
            info.Fields["Overshoot"] = new MValue(overshoot);
            info.Fields["Peak"] = new MValue(peak);
            info.Fields["PeakTime"] = new MValue(t[peakIdx]);
            info.Fields["SettlingTime"] = new MValue(tSettle);
            info.Fields["SteadyState"] = new MValue(yFinal);
            return info;
        }

        /// <summary>State-space model ss(A, B, C, D) almacenado como struct.</summary>
        public static MValue Ss(MValue A, MValue B, MValue C, MValue D, double Ts = 0)
        {
            var st = MValue.NewStruct();
            st.Fields["A"] = A;
            st.Fields["B"] = B;
            st.Fields["C"] = C;
            st.Fields["D"] = D;
            st.Fields["Ts"] = new MValue(Ts);
            st.Fields["type"] = new MValue("ss");
            return st;
        }
        /// <summary>tf → ss en forma canónica controlable.</summary>
        public static MValue Tf2Ss(double[] num, double[] den)
        {
            int n = den.Length - 1;
            if (n < 1) throw new MatlabRuntimeException("tf2ss: orden 0");
            double a_n = den[0];
            var aNorm = new double[n + 1];
            for (int i = 0; i <= n; i++) aNorm[i] = den[i] / a_n;
            var bNorm = new double[num.Length];
            for (int i = 0; i < num.Length; i++) bNorm[i] = num[i] / a_n;
            // Companion (controlable) form A
            var A = new MValue(n, n);
            for (int i = 0; i < n - 1; i++) A.Set(i, i + 1, 1);
            for (int j = 0; j < n; j++) A.Set(n - 1, j, -aNorm[n - j]);
            // B = [0; ...; 1]
            var B = new MValue(n, 1);
            B.Set(n - 1, 0, 1);
            // C, D
            var bFull = new double[n + 1];
            int offset = n + 1 - bNorm.Length;
            for (int i = 0; i < bNorm.Length; i++) bFull[i + offset] = bNorm[i];
            double D = bFull[0];
            var Cmat = new MValue(1, n);
            for (int j = 0; j < n; j++) Cmat.Set(0, j, bFull[n - j] - D * aNorm[n - j]);
            return Ss(A, B, Cmat, new MValue(D));
        }
        /// <summary>ss → tf vía característica + numerator polynomial.</summary>
        public static MValue Ss2Tf(MValue ssModel)
        {
            var A = ssModel.Fields["A"];
            var B = ssModel.Fields["B"];
            var Cmat = ssModel.Fields["C"];
            var D = ssModel.Fields["D"];
            int n = A.Rows;
            // den(s) = det(sI - A) — vía coeficientes Faddeev-Leverrier
            // num(s) = C·adj(sI-A)·B + D·det(sI-A)
            // MVP: usar transformación inversa con coeficientes hallados via expansión polinómica
            // Implementación simple: evaluar H(s) en N puntos y ajustar polinomios
            var ssRoots = SolveDet(A, n);
            // Construir den polinómico desde raíces
            var den = PolyFromRoots(ssRoots.ToArray());
            // Para num: H(s) = C(sI - A)^-1 B + D, evaluar en puntos y resolver Vandermonde
            int Npts = n + 2;
            var sPts = new double[Npts];
            for (int i = 0; i < Npts; i++) sPts[i] = i + 2.5;   // puntos arbitrarios no-coincidentes con polos
            var H_vals = new double[Npts];
            for (int p = 0; p < Npts; p++)
            {
                double s = sPts[p];
                // Construir (sI - A)
                var M = new MValue(n, n);
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        M.Set(i, j, (i == j ? s : 0) - A.At(i, j));
                MValue invMb;
                try { invMb = MatlabLinAlg.Linsolve(M, B); }
                catch { H_vals[p] = 0; continue; }
                double cAv = 0;
                for (int j = 0; j < n; j++) cAv += Cmat.At(0, j) * invMb.At(j, 0);
                H_vals[p] = cAv + D.Scalar;
            }
            // num(s) = H(s) · den(s)
            var numVals = new double[Npts];
            for (int p = 0; p < Npts; p++)
            {
                double denVal = 0;
                double sp = 1;
                for (int k = den.Length - 1; k >= 0; k--) { denVal += den[k] * sp; sp *= sPts[p]; }
                // den está como [coef_n, ..., coef_0] — recalcular correctamente
                double dv = 0;
                for (int i = 0; i < den.Length; i++) dv = dv * sPts[p] + den[i];
                numVals[p] = H_vals[p] * dv;
            }
            // Ajustar polinomio de grado ≤ n a numVals — Vandermonde
            var V = new MValue(Npts, n + 1);
            for (int i = 0; i < Npts; i++)
                for (int j = 0; j <= n; j++) V.Set(i, j, Math.Pow(sPts[i], n - j));
            var yCol = new MValue(Npts, 1);
            for (int i = 0; i < Npts; i++) yCol.Set(i, 0, numVals[i]);
            var p_fit = MatlabLinAlg.Linsolve(V, yCol);
            var num = new double[n + 1];
            for (int i = 0; i <= n; i++) num[i] = p_fit.At(i, 0);
            // Trim leading zeros
            int trim = 0;
            while (trim < num.Length - 1 && Math.Abs(num[trim]) < 1e-10) trim++;
            var numFinal = new double[num.Length - trim];
            Array.Copy(num, trim, numFinal, 0, numFinal.Length);
            return Tf(numFinal, den);
        }
        private static System.Collections.Generic.List<double> SolveDet(MValue A, int n)
        {
            // Eigenvalores reales de A (filtramos imaginarios)
            var coefs = new double[n + 1];
            // Polinomio característico vía Faddeev-Leverrier (simple)
            var pCoefs = new double[n + 1];
            pCoefs[0] = 1;
            var M = new MValue(n, n);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) M.Set(i, j, 0);
            for (int k = 1; k <= n; k++)
            {
                // M_k = A * M_{k-1} + p_{k-1} * I  (con p_0 = 1)
                var Mnew = new MValue(n, n);
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        double s = 0;
                        for (int kk = 0; kk < n; kk++) s += A.At(i, kk) * M.At(kk, j);
                        Mnew.Set(i, j, s + (i == j ? pCoefs[k - 1] : 0));
                    }
                // p_k = -trace(A * M_k) / k
                double tr = 0;
                for (int i = 0; i < n; i++)
                    for (int kk = 0; kk < n; kk++) tr += A.At(i, kk) * Mnew.At(kk, i);
                pCoefs[k] = -tr / k;
                M = Mnew;
            }
            // pCoefs es char polynomial [1, p1, p2, ..., pn]
            var roots = new System.Collections.Generic.List<double>();
            var allRoots = DurandKernerComplex(pCoefs);
            foreach (var (re, im) in allRoots)
                roots.Add(re);   // mantener como reales (imaginarios filtrados visualmente)
            return roots;
        }
        internal static (double re, double im)[] DurandKernerComplex(double[] coefs)
        {
            int deg = coefs.Length - 1;
            while (deg > 0 && Math.Abs(coefs[coefs.Length - 1 - deg]) < 1e-15) deg--;
            if (deg <= 0) return new (double, double)[0];
            var rRe = new double[deg]; var rIm = new double[deg];
            double bound = 0;
            for (int i = 0; i < deg; i++) bound = Math.Max(bound, Math.Abs(coefs[coefs.Length - 1 - i] / coefs[0]));
            bound = 1 + bound;
            for (int k = 0; k < deg; k++)
            {
                double ang = 2 * Math.PI * k / deg + 0.4;
                rRe[k] = bound * Math.Cos(ang) / 2;
                rIm[k] = bound * Math.Sin(ang) / 2;
            }
            for (int iter = 0; iter < 200; iter++)
            {
                for (int k = 0; k < deg; k++)
                {
                    double pRe = 0, pIm = 0;
                    for (int i = 0; i < coefs.Length; i++)
                    {
                        double nRe = pRe * rRe[k] - pIm * rIm[k] + coefs[i];
                        double nIm = pRe * rIm[k] + pIm * rRe[k];
                        pRe = nRe; pIm = nIm;
                    }
                    double dRe = 1, dIm = 0;
                    for (int j = 0; j < deg; j++)
                    {
                        if (j == k) continue;
                        double dx = rRe[k] - rRe[j], dy = rIm[k] - rIm[j];
                        double nRe = dRe * dx - dIm * dy;
                        double nIm = dRe * dy + dIm * dx;
                        dRe = nRe; dIm = nIm;
                    }
                    double denom = dRe * dRe + dIm * dIm;
                    if (denom < 1e-30) continue;
                    double stepRe = (pRe * dRe + pIm * dIm) / denom;
                    double stepIm = (pIm * dRe - pRe * dIm) / denom;
                    rRe[k] -= stepRe; rIm[k] -= stepIm;
                }
            }
            var rs = new (double, double)[deg];
            for (int k = 0; k < deg; k++) rs[k] = (rRe[k], rIm[k]);
            return rs;
        }

        /// <summary>lsim — simulación con entrada arbitraria u(t).</summary>
        public static (MValue t, MValue y) Lsim(double[] num, double[] den, double[] tVec, double[] uVec)
        {
            // Convertir a state-space y simular RK4 con interpolación lineal de u(t)
            int n = den.Length - 1;
            if (n < 1) throw new MatlabRuntimeException("lsim: orden 0");
            double a_n = den[0];
            var aNorm = new double[n + 1];
            for (int i = 0; i <= n; i++) aNorm[i] = den[i] / a_n;
            var bNorm = new double[num.Length];
            for (int i = 0; i < num.Length; i++) bNorm[i] = num[i] / a_n;
            var A = new double[n, n];
            for (int i = 0; i < n - 1; i++) A[i, i + 1] = 1;
            for (int j = 0; j < n; j++) A[n - 1, j] = -aNorm[n - j];
            var B = new double[n]; B[n - 1] = 1;
            var bFull = new double[n + 1];
            int offset = n + 1 - bNorm.Length;
            for (int i = 0; i < bNorm.Length; i++) bFull[i + offset] = bNorm[i];
            double D = bFull[0];
            var C = new double[n];
            for (int j = 0; j < n; j++) C[j] = bFull[n - j] - D * aNorm[n - j];

            int Nt = tVec.Length;
            var ts = new MValue(Nt, 1);
            var ys = new MValue(Nt, 1);
            var x = new double[n];
            double UAt(double t)
            {
                if (t <= tVec[0]) return uVec[0];
                if (t >= tVec[Nt - 1]) return uVec[Nt - 1];
                int lo = 0, hi = Nt - 1;
                while (hi - lo > 1) { int mid = (lo + hi) / 2; if (tVec[mid] <= t) lo = mid; else hi = mid; }
                double tt = (t - tVec[lo]) / (tVec[hi] - tVec[lo]);
                return uVec[lo] + tt * (uVec[hi] - uVec[lo]);
            }
            double[] Deriv(double[] xv, double t)
            {
                var dx = new double[n];
                double u = UAt(t);
                for (int i = 0; i < n; i++)
                {
                    double s = B[i] * u;
                    for (int j = 0; j < n; j++) s += A[i, j] * xv[j];
                    dx[i] = s;
                }
                return dx;
            }
            double[] AddS(double[] a, double[] b, double s)
            {
                var r = new double[a.Length];
                for (int i = 0; i < a.Length; i++) r[i] = a[i] + s * b[i];
                return r;
            }
            ts.Set(0, 0, tVec[0]);
            double y0 = D * UAt(tVec[0]);
            for (int j = 0; j < n; j++) y0 += C[j] * x[j];
            ys.Set(0, 0, y0);
            for (int k = 1; k < Nt; k++)
            {
                double t = tVec[k - 1];
                double h = tVec[k] - tVec[k - 1];
                var k1 = Deriv(x, t);
                var k2 = Deriv(AddS(x, k1, h / 2), t + h / 2);
                var k3 = Deriv(AddS(x, k2, h / 2), t + h / 2);
                var k4 = Deriv(AddS(x, k3, h), t + h);
                for (int j = 0; j < n; j++) x[j] += h * (k1[j] + 2 * k2[j] + 2 * k3[j] + k4[j]) / 6;
                ts.Set(k, 0, tVec[k]);
                double yv = D * UAt(tVec[k]);
                for (int j = 0; j < n; j++) yv += C[j] * x[j];
                ys.Set(k, 0, yv);
            }
            return (ts, ys);
        }

        /// <summary>Discretiza un sistema continuo via Tustin (bilinear).</summary>
        public static (double[] num, double[] den) C2dTustin(double[] num, double[] den, double Ts)
        {
            // Tustin: s = (2/T) · (z-1)/(z+1) — sustituir en H(s) = num(s)/den(s)
            // Implementación: convertir polinomio en s a polinomio en z via expansión.
            int nn = num.Length - 1, nd = den.Length - 1;
            int order = Math.Max(nn, nd);
            // Coeficientes Pascal para (z-1)^k · (z+1)^(order-k)
            // Polinomio en z resulta: Σ a_i · (z-1)^i · (z+1)^(order-i) · (2/T)^i
            double k_factor = 2.0 / Ts;
            // Helper: polinomio (z-1)^p
            double[] PolyZm1(int p)
            {
                var r = new double[] { 1 };
                for (int i = 0; i < p; i++)
                {
                    var newR = new double[r.Length + 1];
                    for (int j = 0; j < r.Length; j++) { newR[j] += r[j]; newR[j + 1] -= r[j]; }
                    r = newR;
                }
                return r;
            }
            double[] PolyZp1(int p)
            {
                var r = new double[] { 1 };
                for (int i = 0; i < p; i++)
                {
                    var newR = new double[r.Length + 1];
                    for (int j = 0; j < r.Length; j++) { newR[j] += r[j]; newR[j + 1] += r[j]; }
                    r = newR;
                }
                return r;
            }
            double[] PolyTransform(double[] coefs, int n)
            {
                // coefs son [a_n, a_{n-1}, ..., a_0] (decreciente en s)
                // resultado: polinomio en z de grado n
                var result = new double[n + 1];
                for (int i = 0; i < coefs.Length; i++)
                {
                    int p_s = coefs.Length - 1 - i;   // potencia de s
                    double coef = coefs[i] * Math.Pow(k_factor, p_s);
                    var P1 = PolyZm1(p_s);
                    var P2 = PolyZp1(n - p_s);
                    var prod = new double[P1.Length + P2.Length - 1];
                    for (int p = 0; p < P1.Length; p++)
                        for (int q = 0; q < P2.Length; q++)
                            prod[p + q] += P1[p] * P2[q];
                    for (int k = 0; k < prod.Length; k++) result[k] += coef * prod[k];
                }
                return result;
            }
            var numZ = PolyTransform(num, order);
            var denZ = PolyTransform(den, order);
            // Normalizar para que primer coef de denZ sea 1
            double scale = denZ[0];
            if (Math.Abs(scale) > 1e-15)
            {
                for (int i = 0; i < numZ.Length; i++) numZ[i] /= scale;
                for (int i = 0; i < denZ.Length; i++) denZ[i] /= scale;
            }
            return (numZ, denZ);
        }

        /// <summary>Series: H1·H2 — multiplica polinomios.</summary>
        public static (double[] num, double[] den) Series(double[] n1, double[] d1, double[] n2, double[] d2)
        {
            return (PolyMul(n1, n2), PolyMul(d1, d2));
        }
        /// <summary>Parallel: H1 + H2 = (n1·d2 + n2·d1) / (d1·d2).</summary>
        public static (double[] num, double[] den) Parallel(double[] n1, double[] d1, double[] n2, double[] d2)
        {
            var nNew = PolyAdd(PolyMul(n1, d2), PolyMul(n2, d1));
            var dNew = PolyMul(d1, d2);
            return (nNew, dNew);
        }
        /// <summary>Feedback: H/(1+H·G) con G = (1, 1) si no se da.</summary>
        public static (double[] num, double[] den) Feedback(double[] n1, double[] d1, double[] n2, double[] d2, int sign = -1)
        {
            // Closed-loop = n1·d2 / (d1·d2 - sign·n1·n2)
            var newNum = PolyMul(n1, d2);
            var loopProd = PolyMul(n1, n2);
            var denProd = PolyMul(d1, d2);
            var newDen = sign < 0 ? PolyAdd(denProd, loopProd) : PolySub(denProd, loopProd);
            return (newNum, newDen);
        }
        private static double[] PolyMul(double[] a, double[] b)
        {
            var r = new double[a.Length + b.Length - 1];
            for (int i = 0; i < a.Length; i++)
                for (int j = 0; j < b.Length; j++)
                    r[i + j] += a[i] * b[j];
            return r;
        }
        private static double[] PolyAdd(double[] a, double[] b)
        {
            int n = Math.Max(a.Length, b.Length);
            var r = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (i < a.Length) r[a.Length - 1 - i + (n - a.Length)] += a[a.Length - 1 - i];
            }
            // Más simple: alinear desde la derecha
            r = new double[n];
            for (int i = 0; i < a.Length; i++) r[n - a.Length + i] += a[i];
            for (int i = 0; i < b.Length; i++) r[n - b.Length + i] += b[i];
            return r;
        }
        private static double[] PolySub(double[] a, double[] b)
        {
            int n = Math.Max(a.Length, b.Length);
            var r = new double[n];
            for (int i = 0; i < a.Length; i++) r[n - a.Length + i] += a[i];
            for (int i = 0; i < b.Length; i++) r[n - b.Length + i] -= b[i];
            return r;
        }
    }
}
