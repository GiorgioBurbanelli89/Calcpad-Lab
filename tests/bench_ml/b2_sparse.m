% #plain
% BENCH 2 - matriz DISPERSA: ensamblado con sparse(i,j,v) + backslash.
% Es el Poisson de 5 puntos en una malla m x m (el mismo patron que sale de un FEM):
% mide el ensamblado disperso y el solver disperso, no el denso.
m = 110;                    % nodos por lado -> n = m^2 incognitas
n = m*m;
nz = 5*n;
ii = zeros(nz,1); jj = zeros(nz,1); vv = zeros(nz,1);
tmin = Inf;
for rep = 1:3
  t0 = tic;
  p = 0;
  for c = 1:m
    for r = 1:m
      k = (c-1)*m + r;
      p = p+1; ii(p)=k; jj(p)=k; vv(p)=4;
      if r > 1, p=p+1; ii(p)=k; jj(p)=k-1; vv(p)=-1; end
      if r < m, p=p+1; ii(p)=k; jj(p)=k+1; vv(p)=-1; end
      if c > 1, p=p+1; ii(p)=k; jj(p)=k-m; vv(p)=-1; end
      if c < m, p=p+1; ii(p)=k; jj(p)=k+m; vv(p)=-1; end
    end
  end
  K = sparse(ii(1:p), jj(1:p), vv(1:p), n, n);
  f = ones(n,1);
  u = K\f;
  s1 = max(u);
  s2 = sum(u);
  s3 = nnz(K);
  tmin = min(tmin, toc(t0));
end
disp(['CHECK b2_umax ' num2str(s1,12)]);
disp(['CHECK b2_usum ' num2str(s2,12)]);
disp(['CHECK b2_nnz ' num2str(s3,12)]);
disp(['CHECK b2_res ' num2str(norm(K*u-f),6)]);
disp(['CHECK t_seg ' num2str(tmin,6)]);
