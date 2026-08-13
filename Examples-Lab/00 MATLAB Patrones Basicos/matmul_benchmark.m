% #md
% # Matrix multiplication benchmark
% A replica of the test published by **Nedelcho Ganchovski** in Calcpad. Here it runs
% on the **MATLAB engine of Hekatan Lab**, which delegates dense linear algebra to
% **Intel MKL**. Machine: **Intel i7-14650HX** (16 cores / 24 threads), 16 GB.
% #endmd

sizes = [1000 2000 4000];
times = zeros(1,3);
for k = 1:3
    n = sizes(k);
    A = rand(n,n);
    B = rand(n,n);
    C = A*B;                 % warmup: spins up the MKL threads (not timed)
    tic;
    C = A*B;                 % timed multiplication
    times(k) = round(toc*10000)/10000;   % round to 4 decimals for the table
    chk = C(1,1) + C(n,n);   % using the result stops the JIT from eliminating it
end

% #md
% ## Results
% | Size | Hekatan Lab (Intel MKL) | Calcpad 7.7.0 (Nedelcho) |
% | :-- | :--: | :--: |
% | 1000 x 1000 | @{times(1)} s | 0.056 s |
% | 2000 x 2000 | @{times(2)} s | 0.351 s |
% | 4000 x 4000 | @{times(3)} s | 2.586 s |
%
% *Hekatan Lab on i7-14650HX (16 cores) - Calcpad on i7-1065G7 (4 cores).*
% #endmd
