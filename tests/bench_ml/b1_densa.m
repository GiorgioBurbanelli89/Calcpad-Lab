% #plain
% BENCH 1 - algebra DENSA: matmul + Cholesky + backslash + traza.
% El mismo .m corre en MATLAB R2017a y en Hekatan Lab; los CHECK deben coincidir
% y t_seg dice cual motor va mas rapido en BLAS/LAPACK denso.
% Sin rand: la matriz se construye con una formula, asi los dos motores ven EXACTAMENTE
% los mismos numeros (rand con otra semilla haria incomparables los CHECK).
n = 400;
[I,J] = ndgrid(1:n,1:n);
B = sin(I.*J);
b = (1:n)';
tmin = Inf;
for rep = 1:3
  t0 = tic;
  A = B'*B + n*eye(n);      % SPD por construccion
  R = chol(A);              % LAPACK dpotrf
  x = A\b;                  % LAPACK dposv/dgesv
  s1 = sum(diag(R));
  s2 = trace(A);
  s3 = x(n);
  s4 = norm(x);
  tmin = min(tmin, toc(t0));
end
disp(['CHECK b1_cholsum ' num2str(s1,12)]);
disp(['CHECK b1_traza ' num2str(s2,12)]);
disp(['CHECK b1_xn ' num2str(s3,12)]);
disp(['CHECK b1_normx ' num2str(s4,12)]);
disp(['CHECK b1_res ' num2str(norm(A*x-b),6)]);
disp(['CHECK t_seg ' num2str(tmin,6)]);
