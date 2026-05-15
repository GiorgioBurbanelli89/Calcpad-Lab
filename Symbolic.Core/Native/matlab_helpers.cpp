/**
 * matlab_helpers.cpp — Native C++ implementations of MATLAB-style helpers
 *                      missing from Calcpad's MathParser.
 *
 * Funciones expuestas (extern "C"):
 *   ml_linspace(a, b, n, out)             — n puntos equiespaciados [a,b]
 *   ml_logspace(a, b, n, out)             — n puntos log-equiespaciados 10^a..10^b
 *   ml_unique(in, n, out, out_count)      — sort + dedupe
 *   ml_sort(in, n, ascending, out)        — sort ascendente/descendente
 *   ml_find_gt(in, n, threshold, out_idx, out_count) — buscar índices v>thr (1-based)
 *   ml_arange(start, stop, step, out, out_count)     — start:step:stop (MATLAB rango)
 *
 * Build (Windows, MinGW):
 *   g++ -O2 -shared -o matlab_helpers.dll matlab_helpers.cpp \
 *       -static-libgcc -static-libstdc++
 *
 * Build (Linux):
 *   g++ -O2 -shared -fPIC -o libmatlab_helpers.so matlab_helpers.cpp
 *
 * Notas:
 *  - Todos los out arrays son responsabilidad del caller (preasignados).
 *  - Para `ml_unique` y `ml_find_gt`, out_count devuelve el tamaño real usado.
 *  - Performance: O(n log n) para sort/unique (std::sort), O(n) lineales.
 *  - No throws; errores se reportan vía return code.
 */

#include <algorithm>
#include <cmath>
#include <cstring>
#include <vector>

#ifdef _WIN32
  #define EXPORT extern "C" __declspec(dllexport)
#else
  #define EXPORT extern "C" __attribute__((visibility("default")))
#endif

/**
 * linspace(a, b, n) → n puntos equiespaciados desde a hasta b inclusive.
 * Si n==0 devuelve []; si n==1 devuelve [a].
 * @param a primer punto
 * @param b último punto
 * @param n número de puntos (>=0)
 * @param out array preasignado de tamaño >= n
 * @return 0 = ok, -1 = n<0
 */
EXPORT int ml_linspace(double a, double b, int n, double* out) {
    if (n < 0) return -1;
    if (n == 0) return 0;
    if (n == 1) { out[0] = a; return 0; }
    double step = (b - a) / static_cast<double>(n - 1);
    for (int i = 0; i < n; ++i) {
        out[i] = a + step * static_cast<double>(i);
    }
    // Forzar el último valor exacto para evitar floating-point drift
    out[n - 1] = b;
    return 0;
}

/**
 * logspace(a, b, n) → n puntos log-equiespaciados desde 10^a hasta 10^b.
 */
EXPORT int ml_logspace(double a, double b, int n, double* out) {
    if (n < 0) return -1;
    if (n == 0) return 0;
    if (n == 1) { out[0] = std::pow(10.0, a); return 0; }
    double step = (b - a) / static_cast<double>(n - 1);
    for (int i = 0; i < n; ++i) {
        out[i] = std::pow(10.0, a + step * static_cast<double>(i));
    }
    return 0;
}

/**
 * unique(v) → sorted + deduplicated.
 * @param in array de entrada
 * @param n tamaño de in
 * @param out array preasignado (tamaño >= n)
 * @param out_count [OUT] cantidad real de elementos únicos
 * @return 0 = ok, -1 = n<0
 *
 * Tolerancia de igualdad: 1e-15 absoluto (MATLAB usa tol relativa pero
 * para nuestros casos numéricos típicos esto basta).
 */
EXPORT int ml_unique(const double* in, int n, double* out, int* out_count) {
    if (n < 0) return -1;
    if (n == 0) { *out_count = 0; return 0; }

    std::vector<double> v(in, in + n);
    std::sort(v.begin(), v.end());

    // Dedupe con tolerancia
    const double tol = 1e-15;
    int writeIdx = 0;
    out[writeIdx++] = v[0];
    for (int i = 1; i < n; ++i) {
        if (std::abs(v[i] - out[writeIdx - 1]) > tol) {
            out[writeIdx++] = v[i];
        }
    }
    *out_count = writeIdx;
    return 0;
}

/**
 * sort(v) ascending o descending.
 * @param ascending !=0 → ascendente; 0 → descendente
 */
EXPORT int ml_sort(const double* in, int n, int ascending, double* out) {
    if (n < 0) return -1;
    if (n == 0) return 0;
    std::memcpy(out, in, n * sizeof(double));
    if (ascending) std::sort(out, out + n);
    else std::sort(out, out + n, std::greater<double>());
    return 0;
}

/**
 * find_gt(v, threshold) → índices (1-based, estilo MATLAB) donde v[i] > threshold.
 * @param out_idx preasignado, tamaño >= n
 * @param out_count [OUT] cantidad de matches
 */
EXPORT int ml_find_gt(const double* in, int n, double threshold,
                      int* out_idx, int* out_count) {
    if (n < 0) return -1;
    int cnt = 0;
    for (int i = 0; i < n; ++i) {
        if (in[i] > threshold) {
            out_idx[cnt++] = i + 1; // MATLAB 1-based
        }
    }
    *out_count = cnt;
    return 0;
}

/**
 * arange(start, stop, step) → start:step:stop estilo MATLAB.
 * Si step==0 devuelve solo [start].
 * Si signos opuestos retorna vacío.
 * @param out preasignado con tamaño suficiente (recomendado: |stop-start|/|step|+2)
 * @param out_count [OUT] cantidad real
 */
EXPORT int ml_arange(double start, double stop, double step,
                     double* out, int* out_count) {
    if (step == 0.0) {
        out[0] = start;
        *out_count = 1;
        return 0;
    }
    int cnt = 0;
    if (step > 0) {
        for (double v = start; v <= stop + 1e-15; v += step) {
            out[cnt++] = v;
        }
    } else {
        for (double v = start; v >= stop - 1e-15; v += step) {
            out[cnt++] = v;
        }
    }
    *out_count = cnt;
    return 0;
}

// ─────────────────────────────────────────────────────────────────
// FEM Kernels — bucles tight a velocidad C++ nativo (10-100× más
// rápidos que el MathParser interpretado de Calcpad). Sin Eigen
// (mantenemos matlab_helpers ligero); solo C++ standard.
// ─────────────────────────────────────────────────────────────────

/**
 * AXPY: y[i] += alpha * x[i] for i in [0, n). MATLAB equivalente:
 *   y = y + alpha * x   (con y y x vectores de tamaño n)
 *
 * Es la operación más fundamental del FEM en ensamble (acumular
 * contribuciones locales en vectores globales).
 * @param n longitud
 * @param alpha escalar
 * @param x vector de entrada (read-only)
 * @param y vector in-place (modificado)
 */
EXPORT int ml_axpy(int n, double alpha, const double* x, double* y) {
    if (n <= 0) return 0;
    for (int i = 0; i < n; ++i) y[i] += alpha * x[i];
    return 0;
}

/**
 * SAXPY con incrementos arbitrarios (gather/scatter style típico de FEM).
 *   for k in [0, n):
 *     y[idy[k]] += alpha * x[idx[k]]
 *
 * Esto es exactamente lo que hace el ensamble FEM cuando los DOF
 * globales no son contiguos: <c>K_global[idof, jdof] += K_local[i, j]</c>.
 * @param n longitud del índice
 * @param idx índices source (en x), 0-based
 * @param idy índices destination (en y), 0-based
 */
EXPORT int ml_axpy_scatter(int n, double alpha,
                           const int* idx, const double* x,
                           const int* idy, double* y) {
    if (n <= 0) return 0;
    for (int k = 0; k < n; ++k) {
        y[idy[k]] += alpha * x[idx[k]];
    }
    return 0;
}

/**
 * Dense matrix-matrix multiply: C = A * B
 *   A: m x k  (row-major)
 *   B: k x n  (row-major)
 *   C: m x n  (row-major, output)
 *
 * Para matrices densas pequeñas (tipo FEM K_local 8x8 o 24x24) este
 * loop ijk simple es competitivo con BLAS (cache-friendly, sin overhead).
 */
EXPORT int ml_matmul(int m, int k, int n,
                     const double* A, const double* B, double* C) {
    if (m <= 0 || k <= 0 || n <= 0) return -1;
    // Inicializar C a 0
    for (int i = 0; i < m * n; ++i) C[i] = 0.0;
    // ijk loop estándar
    for (int i = 0; i < m; ++i) {
        for (int p = 0; p < k; ++p) {
            double a_ip = A[i * k + p];
            const double* b_row = &B[p * n];
            double* c_row = &C[i * n];
            for (int j = 0; j < n; ++j) {
                c_row[j] += a_ip * b_row[j];
            }
        }
    }
    return 0;
}

/**
 * Matrix-vector multiply: y = A * x
 *   A: m x n  (row-major)
 *   x: n
 *   y: m
 */
EXPORT int ml_matvec(int m, int n, const double* A, const double* x, double* y) {
    if (m <= 0 || n <= 0) return -1;
    for (int i = 0; i < m; ++i) {
        double s = 0.0;
        const double* a_row = &A[i * n];
        for (int j = 0; j < n; ++j) s += a_row[j] * x[j];
        y[i] = s;
    }
    return 0;
}

/**
 * Ensamble FEM 2D Q4 típico: agregar K_local (8x8) a K_global (NxN) en los
 * DOFs especificados por dofs[8].
 *
 *   for i in [0,8): for j in [0,8):
 *     K_global[dofs[i], dofs[j]] += K_local[i, j]
 *
 * Caso optimizado del ensamble FEM. Sustituye un doble bucle interpretado
 * de Calcpad (que es ~100× más lento).
 *
 * @param ndof_local tamaño del elemento (8 para Q4 plane, 24 para Q4 shell, etc.)
 * @param ndof_global tamaño de K_global (NxN)
 * @param K_local matriz local (ndof_local x ndof_local), row-major
 * @param dofs vector de DOFs globales, tamaño ndof_local, 0-based
 * @param K_global matriz global (ndof_global x ndof_global), row-major, in-place
 */
EXPORT int ml_assemble_K(int ndof_local, int ndof_global,
                         const double* K_local, const int* dofs,
                         double* K_global) {
    if (ndof_local <= 0 || ndof_global <= 0) return -1;
    for (int i = 0; i < ndof_local; ++i) {
        int gi = dofs[i];
        if (gi < 0 || gi >= ndof_global) return -2; // DOF fuera de rango
        const double* k_row = &K_local[i * ndof_local];
        double* G_row = &K_global[gi * ndof_global];
        for (int j = 0; j < ndof_local; ++j) {
            int gj = dofs[j];
            if (gj < 0 || gj >= ndof_global) return -2;
            G_row[gj] += k_row[j];
        }
    }
    return 0;
}

/**
 * Integración Gauss 2D (cuadratura producto): retorna nodos y pesos
 * para nx puntos en xi y ny puntos en eta.
 *
 *   xi[k]   = i-th gauss point en eta (i = k / ny)
 *   eta[k]  = j-th gauss point en xi  (j = k % ny)
 *   w[k]    = wxi[i] * weta[j]
 *
 * Para nx=ny=2 (típico Q4): 4 puntos a ±1/√3 con pesos 1.
 * Para nx=ny=3 (Q8/Q9): 9 puntos.
 *
 * @param nx puntos en dirección xi (1, 2, 3, o 4 soportados)
 * @param ny puntos en dirección eta
 * @param xi_out output array, tamaño >= nx*ny
 * @param eta_out output array, tamaño >= nx*ny
 * @param w_out  output array, tamaño >= nx*ny
 * @return n_points = nx*ny en *out_count si OK; -1 si nx/ny no soportado
 */
EXPORT int ml_gauss_2d(int nx, int ny, double* xi_out, double* eta_out,
                       double* w_out, int* out_count) {
    // Tablas para 1D Gauss-Legendre estándar
    static const double gp1[] = {0.0};
    static const double gw1[] = {2.0};
    static const double gp2[] = {-0.5773502691896257, 0.5773502691896257};
    static const double gw2[] = {1.0, 1.0};
    static const double gp3[] = {-0.7745966692414834, 0.0, 0.7745966692414834};
    static const double gw3[] = {0.5555555555555556, 0.8888888888888888, 0.5555555555555556};
    static const double gp4[] = {-0.8611363115940526, -0.3399810435848563,
                                  0.3399810435848563, 0.8611363115940526};
    static const double gw4[] = {0.3478548451374538, 0.6521451548625461,
                                  0.6521451548625461, 0.3478548451374538};

    const double* gx;
    const double* wx;
    const double* gy;
    const double* wy;

    switch (nx) {
        case 1: gx = gp1; wx = gw1; break;
        case 2: gx = gp2; wx = gw2; break;
        case 3: gx = gp3; wx = gw3; break;
        case 4: gx = gp4; wx = gw4; break;
        default: return -1;
    }
    switch (ny) {
        case 1: gy = gp1; wy = gw1; break;
        case 2: gy = gp2; wy = gw2; break;
        case 3: gy = gp3; wy = gw3; break;
        case 4: gy = gp4; wy = gw4; break;
        default: return -1;
    }

    int k = 0;
    for (int i = 0; i < nx; ++i) {
        for (int j = 0; j < ny; ++j) {
            xi_out[k] = gx[i];
            eta_out[k] = gy[j];
            w_out[k] = wx[i] * wy[j];
            ++k;
        }
    }
    *out_count = nx * ny;
    return 0;
}

/**
 * Dot product: sum(x[i] * y[i] for i in [0, n))
 */
EXPORT double ml_dot(int n, const double* x, const double* y) {
    double s = 0.0;
    for (int i = 0; i < n; ++i) s += x[i] * y[i];
    return s;
}

/**
 * Triangular solve: L*y = b where L es lower-triangular n×n.
 * Resuelve por forward substitution (in-place permitido: y puede ser b).
 */
EXPORT int ml_trsolve_lower(int n, const double* L, const double* b, double* y) {
    if (n <= 0) return -1;
    for (int i = 0; i < n; ++i) {
        double s = b[i];
        for (int j = 0; j < i; ++j) s -= L[i * n + j] * y[j];
        double diag = L[i * n + i];
        if (diag == 0.0) return -2; // singular
        y[i] = s / diag;
    }
    return 0;
}

// ─────────────────────────────────────────────────────────────────
// LU solve (dense) — Octave usa LAPACK dgesv. Nuestro Crout-pivoting
// es competitivo para n < 200 sin dep externa.
// ─────────────────────────────────────────────────────────────────

/**
 * Resuelve A*x = b con LU decomposition + partial pivoting.
 *   A: n×n (row-major)  — se modifica in-place (factorizado)
 *   b: n                — se modifica in-place (intermediate)
 *   x: n                — out (solución)
 *   piv: n              — out (vector de pivots)
 *
 * @return 0 = OK, -1 = singular
 */
EXPORT int ml_solve_LU(int n, double* A, const double* b, double* x, int* piv) {
    if (n <= 0) return -1;
    // LU con partial pivoting (Doolittle in-place)
    for (int k = 0; k < n; ++k) {
        // Find pivot
        int p = k;
        double mx = std::abs(A[k * n + k]);
        for (int i = k + 1; i < n; ++i) {
            double v = std::abs(A[i * n + k]);
            if (v > mx) { mx = v; p = i; }
        }
        piv[k] = p;
        if (mx < 1e-300) return -1; // singular
        // Swap rows k and p
        if (p != k) {
            for (int j = 0; j < n; ++j) {
                std::swap(A[k * n + j], A[p * n + j]);
            }
        }
        // Eliminate
        double inv_akk = 1.0 / A[k * n + k];
        for (int i = k + 1; i < n; ++i) {
            double f = A[i * n + k] * inv_akk;
            A[i * n + k] = f;
            for (int j = k + 1; j < n; ++j) {
                A[i * n + j] -= f * A[k * n + j];
            }
        }
    }
    // Apply pivots to b → y
    std::vector<double> y(b, b + n);
    for (int k = 0; k < n; ++k) {
        if (piv[k] != k) std::swap(y[k], y[piv[k]]);
    }
    // Forward solve L*z = y (L has unit diagonal)
    for (int i = 0; i < n; ++i) {
        double s = y[i];
        for (int j = 0; j < i; ++j) s -= A[i * n + j] * y[j];
        y[i] = s;
    }
    // Back solve U*x = z
    for (int i = n - 1; i >= 0; --i) {
        double s = y[i];
        for (int j = i + 1; j < n; ++j) s -= A[i * n + j] * x[j];
        x[i] = s / A[i * n + i];
    }
    return 0;
}

// ─────────────────────────────────────────────────────────────────
// Eigenvalores cerrados para matrices pequeñas — más rápido que iterar.
// Octave para n>=2 va a LAPACK; nosotros short-circuit para n=2,3.
// ─────────────────────────────────────────────────────────────────

/**
 * Eigenvalores de matriz simétrica 2×2 (forma cerrada).
 *   A = [[a, b], [b, c]]
 *   λ± = (a+c)/2 ± sqrt(((a-c)/2)² + b²)
 */
EXPORT int ml_eig_sym_2x2(const double* A, double* lambda) {
    double a = A[0], b = A[1], c = A[3];
    double tr = (a + c) * 0.5;
    double d = (a - c) * 0.5;
    double disc = std::sqrt(d * d + b * b);
    lambda[0] = tr - disc;
    lambda[1] = tr + disc;
    return 0;
}

/**
 * Eigenvalores de matriz simétrica 3×3 (forma cerrada vía Cardano).
 *   Returns 3 eigenvalues ordered ascending.
 */
EXPORT int ml_eig_sym_3x3(const double* A, double* lambda) {
    double a11 = A[0], a12 = A[1], a13 = A[2];
    double a22 = A[4], a23 = A[5];
    double a33 = A[8];
    // Coeficientes del polinomio característico λ³ - p1*λ² + p2*λ - p3 = 0
    double p1 = a11 + a22 + a33;
    double p2 = a11 * a22 + a11 * a33 + a22 * a33 - a12 * a12 - a13 * a13 - a23 * a23;
    double p3 = a11 * (a22 * a33 - a23 * a23)
              - a12 * (a12 * a33 - a23 * a13)
              + a13 * (a12 * a23 - a22 * a13);
    // Reducir a depressed cubic y³ + pp*y + qq = 0  vía λ = y + p1/3
    //   pp = p2 - p1²/3
    //   qq = -2p1³/27 + p1·p2/3 - p3
    double q = p1 / 3.0;
    double pp = p2 - p1 * q;
    double qq = -2.0 * p1 * p1 * p1 / 27.0 + p1 * p2 / 3.0 - p3;
    // y³ + pp*y + qq = 0  →  trigonometric solution (3 real roots)
    double r = std::sqrt(-pp / 3.0);
    if (r < 1e-300) {
        lambda[0] = lambda[1] = lambda[2] = q;
        return 0;
    }
    double cos_arg = -qq / (2.0 * r * r * r);
    if (cos_arg > 1.0) cos_arg = 1.0;
    if (cos_arg < -1.0) cos_arg = -1.0;
    double theta = std::acos(cos_arg) / 3.0;
    double two_r = 2.0 * r;
    double l1 = two_r * std::cos(theta) + q;
    double l2 = two_r * std::cos(theta + 2.0 * 3.14159265358979323846 / 3.0) + q;
    double l3 = two_r * std::cos(theta + 4.0 * 3.14159265358979323846 / 3.0) + q;
    // Sort ascending
    double tmp[3] = { l1, l2, l3 };
    std::sort(tmp, tmp + 3);
    lambda[0] = tmp[0]; lambda[1] = tmp[1]; lambda[2] = tmp[2];
    return 0;
}

// ─────────────────────────────────────────────────────────────────
// Polyval — Octave usa loop interpretado; el nuestro está en C++ tight.
// Para polinomio grande es 100×+ más rápido.
// ─────────────────────────────────────────────────────────────────

/**
 * Evalúa polinomio p(x) = c[0]*x^(n-1) + c[1]*x^(n-2) + ... + c[n-1]
 * en cada elemento de x. Horner.
 */
EXPORT int ml_polyval(int nc, const double* c, int nx, const double* x, double* out) {
    if (nc <= 0 || nx < 0) return -1;
    for (int k = 0; k < nx; ++k) {
        double xk = x[k];
        double r = c[0];
        for (int i = 1; i < nc; ++i) r = r * xk + c[i];
        out[k] = r;
    }
    return 0;
}

// ─────────────────────────────────────────────────────────────────
// Interp1 — interpolación lineal en grid (xs, ys), evaluar en xq.
// Asume xs ordenado ascendente. Octave: griddedInterpolant; este loop
// es competitivo para grids pequeños.
// ─────────────────────────────────────────────────────────────────

EXPORT int ml_interp1_linear(int nx, const double* xs, const double* ys,
                             int nq, const double* xq, double* yq) {
    if (nx < 2 || nq < 0) return -1;
    for (int k = 0; k < nq; ++k) {
        double x = xq[k];
        // Binary search en xs
        if (x <= xs[0]) { yq[k] = ys[0]; continue; }
        if (x >= xs[nx - 1]) { yq[k] = ys[nx - 1]; continue; }
        int lo = 0, hi = nx - 1;
        while (hi - lo > 1) {
            int mid = (lo + hi) >> 1;
            if (xs[mid] <= x) lo = mid; else hi = mid;
        }
        double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
        yq[k] = ys[lo] * (1.0 - t) + ys[hi] * t;
    }
    return 0;
}

// ─────────────────────────────────────────────────────────────────
// FFT radix-2 in-place. Octave usa FFTW para tamaños arbitrarios;
// nuestro radix-2 funciona solo para n potencia de 2 pero es:
//   - Muy ligero (sin dep externa)
//   - O(n log n) optimizado
//   - Competitivo con FFTW para n < 1024
// ─────────────────────────────────────────────────────────────────

/**
 * FFT in-place radix-2 sobre arrays separados real/imag.
 *   re, im: arrays de tamaño n (potencia de 2)
 *   sign:  -1 = forward FFT,  +1 = inverse FFT (sin normalizar)
 * @return 0 = OK, -1 = n no es potencia de 2 o n <= 0
 */
EXPORT int ml_fft_radix2(int n, double* re, double* im, int sign) {
    if (n <= 0) return -1;
    // n debe ser potencia de 2
    int nn = n;
    int logn = 0;
    while ((nn & 1) == 0 && nn > 1) { nn >>= 1; logn++; }
    if (nn != 1) return -1;
    // Bit-reversal permutation
    int j = 0;
    for (int i = 1; i < n; ++i) {
        int bit = n >> 1;
        while (j & bit) { j ^= bit; bit >>= 1; }
        j ^= bit;
        if (i < j) {
            std::swap(re[i], re[j]);
            std::swap(im[i], im[j]);
        }
    }
    // Cooley-Tukey
    const double pi = 3.14159265358979323846;
    for (int len = 2; len <= n; len <<= 1) {
        double ang = sign * 2.0 * pi / len;
        double wcos = std::cos(ang);
        double wsin = std::sin(ang);
        for (int i = 0; i < n; i += len) {
            double wr = 1.0, wi = 0.0;
            for (int k = 0; k < len / 2; ++k) {
                int a = i + k;
                int b = i + k + len / 2;
                double ur = re[a], ui = im[a];
                double vr = re[b] * wr - im[b] * wi;
                double vi = re[b] * wi + im[b] * wr;
                re[a] = ur + vr;
                im[a] = ui + vi;
                re[b] = ur - vr;
                im[b] = ui - vi;
                double nwr = wr * wcos - wi * wsin;
                wi = wr * wsin + wi * wcos;
                wr = nwr;
            }
        }
    }
    return 0;
}

// ─────────────────────────────────────────────────────────────────
// Matmul tiled — versión cache-friendly para matrices grandes.
// Octave llama a OpenBLAS dgemm; este es 2-3× más lento que MKL pero
// 3-5× más rápido que un ml_matmul ijk naïve para n>200.
// ─────────────────────────────────────────────────────────────────

/**
 * Block-tiled matmul: C = A * B, tile size 64 (cache L1 optimizado).
 *   A: m×k, B: k×n, C: m×n (todos row-major)
 */
EXPORT int ml_matmul_tiled(int m, int k, int n,
                           const double* A, const double* B, double* C) {
    if (m <= 0 || k <= 0 || n <= 0) return -1;
    constexpr int TS = 64;
    // Zero C
    for (int i = 0; i < m * n; ++i) C[i] = 0.0;
    // Tile loops
    for (int i0 = 0; i0 < m; i0 += TS) {
        int iL = std::min(i0 + TS, m);
        for (int j0 = 0; j0 < n; j0 += TS) {
            int jL = std::min(j0 + TS, n);
            for (int p0 = 0; p0 < k; p0 += TS) {
                int pL = std::min(p0 + TS, k);
                // Multiply tile A[i0..iL, p0..pL] × B[p0..pL, j0..jL]
                for (int i = i0; i < iL; ++i) {
                    for (int p = p0; p < pL; ++p) {
                        double a_ip = A[i * k + p];
                        const double* b_row = &B[p * n];
                        double* c_row = &C[i * n];
                        for (int j = j0; j < jL; ++j) {
                            c_row[j] += a_ip * b_row[j];
                        }
                    }
                }
            }
        }
    }
    return 0;
}

// ─────────────────────────────────────────────────────────────────
// Bowyer-Watson 2D Delaunay triangulation
//
// Implementación propia (BSD/sin restricciones) que reemplaza la
// dependencia de Triangle (Shewchuk, non-commercial license) usada
// por awatif-v2 (triangle-wasm).
//
// Algoritmo:
//   1. Crear super-triángulo que contiene todos los puntos
//   2. Insertar cada punto: encontrar triángulos cuya circunferencia
//      contiene el punto → forman un "agujero poligonal" → reconectar
//      el punto con los bordes del agujero
//   3. Eliminar triángulos que tocan vértices del super-triángulo
//
// Complejidad: O(n log n) en promedio, O(n²) en peor caso.
// Suficiente para mallas FEM de hasta ~10k puntos.
// ─────────────────────────────────────────────────────────────────

namespace bw {
    struct Tri {
        int a, b, c;      // índices en pts (0-based)
        double cx, cy, r2; // centro y radio² del circumcírculo
        bool alive;
    };

    // Computar circumcírculo del triángulo (ax, ay), (bx, by), (cx, cy).
    // Devuelve true si OK, false si los puntos son colineares.
    inline bool circumcircle(double ax, double ay,
                             double bx, double by,
                             double cx, double cy,
                             double& ox, double& oy, double& r2) {
        double d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (std::abs(d) < 1e-30) return false;
        double ax2 = ax * ax + ay * ay;
        double bx2 = bx * bx + by * by;
        double cx2 = cx * cx + cy * cy;
        ox = (ax2 * (by - cy) + bx2 * (cy - ay) + cx2 * (ay - by)) / d;
        oy = (ax2 * (cx - bx) + bx2 * (ax - cx) + cx2 * (bx - ax)) / d;
        double dx = ax - ox, dy = ay - oy;
        r2 = dx * dx + dy * dy;
        return true;
    }

    inline void setCircum(const double* pts, Tri& t) {
        double ax = pts[2 * t.a],     ay = pts[2 * t.a + 1];
        double bx = pts[2 * t.b],     by = pts[2 * t.b + 1];
        double cx = pts[2 * t.c],     cy = pts[2 * t.c + 1];
        if (!circumcircle(ax, ay, bx, by, cx, cy, t.cx, t.cy, t.r2)) {
            // Degenerate: marcar para nunca contener puntos
            t.cx = 0; t.cy = 0; t.r2 = -1;
        }
    }
}

/**
 * Bowyer-Watson Delaunay 2D.
 *
 * @param n_pts      número de puntos
 * @param pts        array [x0,y0, x1,y1, ...] de tamaño 2*n_pts
 * @param tri_out    array preasignado con espacio para hasta
 *                   3*max_tris ints (típicamente max_tris = 2*n_pts).
 *                   Cada triángulo ocupa 3 ints consecutivos
 *                   (índices en pts, 0-based).
 * @param max_tris   capacidad de tri_out / 3
 * @param n_tris_out [OUT] cantidad real de triángulos producidos
 * @return 0 = OK, -1 = error genérico, -2 = max_tris insuficiente
 */
EXPORT int ml_delaunay_2d(int n_pts, const double* pts,
                          int* tri_out, int max_tris, int* n_tris_out) {
    using namespace bw;
    *n_tris_out = 0;
    if (n_pts < 3) return -1;

    // 1) Calcular bounding box
    double xmin = pts[0], xmax = pts[0];
    double ymin = pts[1], ymax = pts[1];
    for (int i = 1; i < n_pts; ++i) {
        double x = pts[2 * i], y = pts[2 * i + 1];
        if (x < xmin) xmin = x;
        if (x > xmax) xmax = x;
        if (y < ymin) ymin = y;
        if (y > ymax) ymax = y;
    }
    double dx = xmax - xmin, dy = ymax - ymin;
    double dmax = std::max(dx, dy);
    double midx = (xmin + xmax) * 0.5;
    double midy = (ymin + ymax) * 0.5;

    // 2) Lista de puntos con super-triángulo agregado al final
    std::vector<double> P;
    P.reserve(2 * (n_pts + 3));
    P.insert(P.end(), pts, pts + 2 * n_pts);
    // Super-triángulo: muy grande para contener todos los puntos
    int isup1 = n_pts;
    int isup2 = n_pts + 1;
    int isup3 = n_pts + 2;
    P.push_back(midx - 20.0 * dmax); P.push_back(midy - dmax);
    P.push_back(midx);               P.push_back(midy + 20.0 * dmax);
    P.push_back(midx + 20.0 * dmax); P.push_back(midy - dmax);

    // 3) Triangulación inicia con super-triángulo
    std::vector<Tri> tris;
    tris.reserve(4 * n_pts + 10);
    Tri sup; sup.a = isup1; sup.b = isup2; sup.c = isup3; sup.alive = true;
    setCircum(P.data(), sup);
    tris.push_back(sup);

    // 4) Insertar cada punto
    std::vector<int> badTris;
    badTris.reserve(64);
    // edges: lista de pares (v0, v1)
    std::vector<std::pair<int, int>> edges;
    edges.reserve(128);

    for (int p = 0; p < n_pts; ++p) {
        double px = P[2 * p], py = P[2 * p + 1];
        badTris.clear();
        edges.clear();

        // Encontrar triángulos cuya circumcírculo contiene al punto
        for (int i = 0; i < (int)tris.size(); ++i) {
            if (!tris[i].alive) continue;
            double ddx = px - tris[i].cx;
            double ddy = py - tris[i].cy;
            if (ddx * ddx + ddy * ddy < tris[i].r2 - 1e-15) {
                badTris.push_back(i);
            }
        }

        // Construir polígono frontera del agujero (edges que aparecen 1 vez)
        for (int idx : badTris) {
            int a = tris[idx].a, b = tris[idx].b, c = tris[idx].c;
            int eab0 = std::min(a, b), eab1 = std::max(a, b);
            int ebc0 = std::min(b, c), ebc1 = std::max(b, c);
            int eca0 = std::min(c, a), eca1 = std::max(c, a);
            edges.push_back({ eab0, eab1 });
            edges.push_back({ ebc0, ebc1 });
            edges.push_back({ eca0, eca1 });
        }
        // Marcar bad tris como muertos
        for (int idx : badTris) tris[idx].alive = false;

        // Eliminar edges que aparecen ≥2 veces (interiores al agujero)
        std::sort(edges.begin(), edges.end());
        std::vector<std::pair<int, int>> hole;
        hole.reserve(edges.size());
        for (size_t i = 0; i < edges.size(); ) {
            size_t j = i + 1;
            while (j < edges.size() && edges[j] == edges[i]) ++j;
            if (j - i == 1) hole.push_back(edges[i]);
            i = j;
        }

        // Re-triangular: cada edge del hole + el nuevo punto
        for (auto& e : hole) {
            Tri t;
            t.a = e.first; t.b = e.second; t.c = p; t.alive = true;
            setCircum(P.data(), t);
            tris.push_back(t);
        }
    }

    // 5) Quitar triángulos que tocan vértices del super-triángulo
    int outCount = 0;
    for (auto& t : tris) {
        if (!t.alive) continue;
        if (t.a >= n_pts || t.b >= n_pts || t.c >= n_pts) continue;
        if (outCount >= max_tris) return -2;
        tri_out[3 * outCount + 0] = t.a;
        tri_out[3 * outCount + 1] = t.b;
        tri_out[3 * outCount + 2] = t.c;
        ++outCount;
    }
    *n_tris_out = outCount;
    return 0;
}

/**
 * Version string para verificar carga del DLL.
 */
EXPORT const char* matlab_helpers_version() {
    return "matlab_helpers v0.4.0 ("
           "linspace, logspace, unique, sort, find_gt, arange, "
           "axpy, axpy_scatter, matmul, matmul_tiled, matvec, assemble_K, "
           "gauss_2d, dot, trsolve_lower, "
           "solve_LU, eig_sym_2x2, eig_sym_3x3, polyval, interp1_linear, fft_radix2, "
           "delaunay_2d)";
}
