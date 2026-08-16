% #plain
% BENCH 5 - FEM DE VERDAD: voladizo de elasticidad plana con Q4 (Gauss 2x2).
% Es el caso que importa: bucle por elemento + integracion + scatter-add + solve
% disperso, todo junto. Si un motor hace trampa en cualquiera de los cuatro, se ve.
nx = 40; ny = 12;             % elementos
L = 4.0; H = 1.0; t = 1.0;
E = 2.1e5; nu = 0.30;
D = E/(1-nu*nu) * [1 nu 0; nu 1 0; 0 0 (1-nu)/2];   % tension plana
hx = L/nx; hy = H/ny;
nnx = nx+1; nny = ny+1;
nnod = nnx*nny; ndof = 2*nnod;
g = 1/sqrt(3);
gp = [-g -g; g -g; g g; -g g];

% coordenadas: nodo (i,j) -> id (i-1)*nny + j
X = zeros(nnod,1); Y = zeros(nnod,1);
for i = 1:nnx
  for j = 1:nny
    id = (i-1)*nny + j;
    X(id) = (i-1)*hx;
    Y(id) = (j-1)*hy;
  end
end

nel = nx*ny;
CON = zeros(nel,4);
ee = 0;
for i = 1:nx
  for j = 1:ny
    ee = ee+1;
    CON(ee,1) = (i-1)*nny + j;
    CON(ee,2) = i*nny + j;
    CON(ee,3) = i*nny + j+1;
    CON(ee,4) = (i-1)*nny + j+1;
  end
end

tmin = Inf;
for rep = 1:3
  t0 = tic;
  ii = zeros(64*nel,1); jj = zeros(64*nel,1); vv = zeros(64*nel,1);
  p = 0;
  for e = 1:nel
    xe = X(CON(e,:)); ye = Y(CON(e,:));
    ke = zeros(8,8);
    for q = 1:4
      xi = gp(q,1); et = gp(q,2);
      dNx = 0.25*[-(1-et)  (1-et)  (1+et) -(1+et)];   % dN/dxi
      dNe = 0.25*[-(1-xi) -(1+xi)  (1+xi)  (1-xi)];   % dN/deta
      Jm = [dNx*xe  dNx*ye; dNe*xe  dNe*ye];
      dJ = Jm(1,1)*Jm(2,2) - Jm(1,2)*Jm(2,1);
      iJ = [Jm(2,2) -Jm(1,2); -Jm(2,1) Jm(1,1)]/dJ;
      dN = iJ*[dNx; dNe];
      B = zeros(3,8);
      for a = 1:4
        B(1,2*a-1) = dN(1,a);
        B(2,2*a)   = dN(2,a);
        B(3,2*a-1) = dN(2,a);
        B(3,2*a)   = dN(1,a);
      end
      ke = ke + B'*D*B*dJ*t;
    end
    dofs = zeros(1,8);
    for a = 1:4
      dofs(2*a-1) = 2*CON(e,a)-1;
      dofs(2*a)   = 2*CON(e,a);
    end
    for a = 1:8
      for b = 1:8
        p = p+1; ii(p) = dofs(a); jj(p) = dofs(b); vv(p) = ke(a,b);
      end
    end
  end
  K = sparse(ii, jj, vv, ndof, ndof);

  F = zeros(ndof,1);
  ntip = (nnx-1)*nny + round(nny/2);      % nodo medio del extremo libre
  F(2*ntip) = -1000;

  fix = zeros(2*nny,1);                   % empotrado en x = 0
  for j = 1:nny
    fix(2*j-1) = 2*j-1;
    fix(2*j)   = 2*j;
  end
  libre = setdiff((1:ndof)', fix);
  u = zeros(ndof,1);
  u(libre) = K(libre,libre) \ F(libre);
  s1 = max(abs(u));
  s2 = u(2*ntip);
  s3 = sum(u);
  tmin = min(tmin, toc(t0));
end
disp(['CHECK b5_umax ' num2str(s1,12)]);
disp(['CHECK b5_utip ' num2str(s2,12)]);
disp(['CHECK b5_usum ' num2str(s3,12)]);
disp(['CHECK b5_ndof ' num2str(ndof,12)]);
disp(['CHECK t_seg ' num2str(tmin,6)]);
