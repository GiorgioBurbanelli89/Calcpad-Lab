// Benchmark Calcpad Lab (C++ matlab_helpers.dll via P/Invoke) vs GNU Octave
// para operaciones puntuales. Octave debe estar instalado (octave.bat en PATH).
//
// El test es informativo (siempre pasa) — escribe resultados a Console y
// a un archivo de reporte. Para correr SÓLO este test:
//   dotnet test --filter "FullyQualifiedName~OctaveBenchmark"
using System.Diagnostics;
using Calcpad.Core;
using Xunit.Abstractions;

namespace Calcpad.Lab.Tests
{
    public class OctaveBenchmark
    {
        private readonly ITestOutputHelper _o;
        public OctaveBenchmark(ITestOutputHelper o) { _o = o; }

        private static bool OctaveAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo("octave", "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Corre un comando Octave una sola vez (warm-up) midiendo el tiempo
        /// del cálculo (NO del startup), usando tic/toc interno.
        /// </summary>
        private static long OctaveTime(string snippet)
        {
            try
            {
                // Script: tic; <snippet>; t = toc; printf('%d', round(t*1000));
                var arg = $"--eval \"tic; {snippet}; t=toc; printf('%d', round(t*1000));\"";
                var psi = new ProcessStartInfo("octave", arg)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return -1;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(120_000);
                if (int.TryParse(output.Trim(), out int ms)) return ms;
                return -1;
            }
            catch { return -1; }
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Compare_PolyvalLargeArray()
        {
            if (!OctaveAvailable())
            {
                _o.WriteLine("Octave no disponible — skip");
                return;
            }
            int n = 500_000;
            // ─── Calcpad Lab ───
            var c = new double[] { 1, 2, 3, 4, 5 };
            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = i / 1000.0;
            var sw = Stopwatch.StartNew();
            var _ = MatlabHelpersInterop.Polyval(c, x);
            sw.Stop();
            long us_ms = sw.ElapsedMilliseconds;
            // ─── Octave ───
            var snippet = $"x=(0:{n - 1})/1000; c=[1 2 3 4 5]; y=polyval(c,x);";
            long oct_ms = OctaveTime(snippet);
            _o.WriteLine($"polyval (n={n})  ─ Calcpad Lab: {us_ms}ms ─ Octave: {oct_ms}ms");
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Compare_LinspaceLarge()
        {
            if (!OctaveAvailable()) { _o.WriteLine("skip"); return; }
            int n = 1_000_000;
            var sw = Stopwatch.StartNew();
            var _ = MatlabHelpersInterop.Linspace(0, 1, n);
            sw.Stop();
            var snippet = $"v=linspace(0,1,{n});";
            long oct_ms = OctaveTime(snippet);
            _o.WriteLine($"linspace (n={n})  ─ Calcpad Lab: {sw.ElapsedMilliseconds}ms ─ Octave: {oct_ms}ms");
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Compare_FemAssemble1000Elements()
        {
            if (!OctaveAvailable()) { _o.WriteLine("skip"); return; }
            int nElem = 1000;
            int ndofGlobal = 5000;
            int ndofLocal = 8;
            // ─── Calcpad Lab (C++ tight loop) ───
            var Kglob = new double[ndofGlobal * ndofGlobal];
            var Klocal = new double[ndofLocal * ndofLocal];
            for (int i = 0; i < Klocal.Length; i++) Klocal[i] = 1.0 / (i + 1);
            var rnd = new Random(42);
            var dofsAll = new int[nElem * ndofLocal];
            for (int e = 0; e < nElem; e++)
                for (int d = 0; d < ndofLocal; d++)
                    dofsAll[e * ndofLocal + d] = rnd.Next(0, ndofGlobal);

            var sw = Stopwatch.StartNew();
            for (int e = 0; e < nElem; e++)
            {
                var dofs = new int[ndofLocal];
                Array.Copy(dofsAll, e * ndofLocal, dofs, 0, ndofLocal);
                MatlabHelpersInterop.AssembleK(Klocal, dofs, Kglob, ndofGlobal);
            }
            sw.Stop();
            long our_ms = sw.ElapsedMilliseconds;
            // ─── Octave script equivalente ───
            // K_global = zeros(N, N); for e=1:nE, dofs = ...; K_global(dofs, dofs) += K_local; end
            // El K_global(dofs, dofs) += ... es una asignación fancy MUY lenta en Octave interpretado.
            // Generamos los DOFs en Octave también para ser justos.
            var snippet =
                $"N={ndofGlobal}; nE={nElem}; nl={ndofLocal}; " +
                $"K=zeros(N,N); Kl=ones(nl,nl); " +
                $"for e=1:nE, dofs=randi(N,1,nl); K(dofs,dofs)+=Kl; endfor;";
            long oct_ms = OctaveTime(snippet);
            _o.WriteLine($"FEM assemble {nElem}elem×{ndofLocal}DOF  ─ Calcpad Lab: {our_ms}ms ─ Octave: {oct_ms}ms");
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Compare_Matmul_500x500_OctaveWinsLargeViaBLAS()
        {
            // Caso donde OpenBLAS de Octave esperablemente gana (matmul grande
            // con SIMD/AVX). Lo dejamos para identificar el cross-over point.
            if (!OctaveAvailable()) { _o.WriteLine("skip"); return; }
            int n = 500;
            var A = new double[n * n];
            var B = new double[n * n];
            var rnd = new Random(42);
            for (int i = 0; i < n * n; i++) { A[i] = rnd.NextDouble(); B[i] = rnd.NextDouble(); }
            var sw = Stopwatch.StartNew();
            var _ = MatlabHelpersInterop.MatmulTiled(A, n, n, B, n);
            sw.Stop();
            var snippet = $"A=rand({n},{n}); B=rand({n},{n}); C=A*B;";
            long oct_ms = OctaveTime(snippet);
            _o.WriteLine($"matmul {n}×{n}  ─ Calcpad Lab tiled (sin SIMD): {sw.ElapsedMilliseconds}ms ─ Octave (OpenBLAS): {oct_ms}ms");
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Compare_SolveLU_200()
        {
            if (!OctaveAvailable()) { _o.WriteLine("skip"); return; }
            int n = 200;
            var rnd = new Random(42);
            var A = new double[n * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) A[i * n + j] = rnd.NextDouble();
                A[i * n + i] += n;
            }
            var b = new double[n];
            for (int i = 0; i < n; i++) b[i] = rnd.NextDouble();
            var sw = Stopwatch.StartNew();
            var _ = MatlabHelpersInterop.SolveLU(A, n, b);
            sw.Stop();
            // Octave: A\b → LAPACK dgesv
            var snippet = $"A=rand({n},{n})+{n}*eye({n}); b=rand({n},1); x=A\\b;";
            long oct_ms = OctaveTime(snippet);
            _o.WriteLine($"solve LU {n}×{n}  ─ Calcpad Lab: {sw.ElapsedMilliseconds}ms ─ Octave (LAPACK dgesv): {oct_ms}ms");
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Compare_Matmul100x100()
        {
            if (!OctaveAvailable()) { _o.WriteLine("skip"); return; }
            int n = 100;
            var A = new double[n * n];
            var B = new double[n * n];
            var rnd = new Random(42);
            for (int i = 0; i < n * n; i++) { A[i] = rnd.NextDouble(); B[i] = rnd.NextDouble(); }
            var sw = Stopwatch.StartNew();
            var _ = MatlabHelpersInterop.MatmulTiled(A, n, n, B, n);
            sw.Stop();
            long our_ms = sw.ElapsedMilliseconds;
            // Octave: A*B usa OpenBLAS dgemm (muy optimizado).
            var snippet = $"A=rand({n},{n}); B=rand({n},{n}); C=A*B;";
            long oct_ms = OctaveTime(snippet);
            _o.WriteLine($"matmul {n}×{n}  ─ Calcpad Lab tiled: {our_ms}ms ─ Octave (OpenBLAS): {oct_ms}ms");
        }
    }
}
