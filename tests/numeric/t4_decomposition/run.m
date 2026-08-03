% #plain
% CANARIO 4 — decomposition(A): factorizar UNA vez y reusar en dA\b.
% Se usa la Kff REAL del Demo04 (FEM 2D, malla T6), donde la factorizacion SI es
% cara — a diferencia de una tridiagonal, que MATLAB factoriza casi gratis por
% banda y no dejaria ver el beneficio.
%
% En un Newton modificado (misma K, N RHS) el backslash RE-FACTORIZA cada vez.
% decomposition factoriza 1 vez y solo sustituye. MATLAB 2017a NO tiene
% decomposition (R2017b+): alla el bloque cae al catch y paga las N factorizaciones.
% Aqui se demuestra que Lab, reusando la factorizacion, hace en el patron
% multi-solve algo que el oraculo R2017a no puede.
%
% Correctitud: decomposition == backslash del propio Lab (validado vs MATLAB en t3).

[X,Y,ELE,EMAT]=demo04_mesh();
nn=numel(X); ne=size(ELE,1); ndof=2*nn;
MAT=[1.303470e5 0.30; 1.303470e5 0.20];

xmin=min(X); xmax=max(X); ymin=min(Y); fixed=[];
for k=1:nn
  if abs(Y(k)-ymin)<1e-4; fixed=[fixed 2*k-1 2*k]; end
  if abs(X(k)-xmin)<1e-4 || abs(X(k)-xmax)<1e-4; fixed=[fixed 2*k-1]; end
end
fixed=unique(fixed); free=setdiff(1:ndof,fixed);

gp=[1/6 1/6; 2/3 1/6; 1/6 2/3]; gw=[1/6 1/6 1/6];
Bc=cell(ne,3); dJw=zeros(ne,3); edof=zeros(ne,12);
for e=1:ne
  nd=ELE(e,:); xy=[X(nd) Y(nd)];
  for a=1:6; edof(e,2*a-1)=2*nd(a)-1; edof(e,2*a)=2*nd(a); end
  for q=1:3
    [~,dL1,dL2]=t6shp(gp(q,1),gp(q,2));
    J=[dL1'*xy(:,1) dL1'*xy(:,2); dL2'*xy(:,1) dL2'*xy(:,2)]; Ji=J\eye(2);
    dNx=Ji(1,1)*dL1+Ji(1,2)*dL2; dNy=Ji(2,1)*dL1+Ji(2,2)*dL2;
    B=zeros(3,12); for a=1:6; B(1,2*a-1)=dNx(a); B(2,2*a)=dNy(a); B(3,2*a-1)=dNy(a); B(3,2*a)=dNx(a); end
    Bc{e,q}=B; dJw(e,q)=det(J)*gw(q);
  end
end
D3=cell(2,1);
for mm=1:2; E=MAT(mm,1); nu=MAT(mm,2); f=E/((1+nu)*(1-2*nu));
  D3{mm}=f*[1-nu nu 0; nu 1-nu 0; 0 0 (1-2*nu)/2]; end

K=sparse(ndof,ndof);
for e=1:ne; Dm=D3{EMAT(e)}; Ke=zeros(12,12);
  for q=1:3; B=Bc{e,q}; Ke=Ke+B'*Dm*B*dJw(e,q); end
  d=edof(e,:); K(d,d)=K(d,d)+Ke; end
Kff=K(free,free); n=size(Kff,1);

N=100;                        % RHS distintos (como iteraciones de Newton)

% --- referencia: backslash normal, re-factoriza en cada solve ---
tic;
acc1=0;
for i=1:N
  b=zeros(n,1); b(1)=1; b(n)=i*0.01;
  x=Kff\b;
  acc1=acc1+x(1)+x(n);
end
t_bs=toc;
disp(['CHECK acc_backslash ' num2str(acc1,12)]);
disp(['CHECK t_backslash ' num2str(t_bs,6)]);

% --- decomposition: factorizar 1 vez, reusar (no existe en R2017a) ---
try
  dK=decomposition(Kff);
  tic;
  acc2=0;
  for i=1:N
    b=zeros(n,1); b(1)=1; b(n)=i*0.01;
    x=dK\b;
    acc2=acc2+x(1)+x(n);
  end
  t_dc=toc;
  disp(['CHECK acc_decomp ' num2str(acc2,12)]);
  disp(['CHECK t_decomp ' num2str(t_dc,6)]);
  disp(['CHECK dif_decomp_vs_backslash ' num2str(abs(acc1-acc2),12)]);
  disp(['CHECK speedup_vs_backslash ' num2str(t_bs/max(t_dc,1e-9),4)]);
catch
  disp('decomposition no disponible en este motor (R2017a) — solo se midio backslash');
end
