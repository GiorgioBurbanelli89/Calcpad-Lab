function femcanary()
% CANARIO 3 — FEM elastoplastico completo (talud Demo04 de GEO5, SRM c-phi).
% Es el test mas valioso porque en UNA corrida ejercita:
%   indexado vectorial u(d'), cell arrays Bc{e,q}, funciones con 11 parametros,
%   constantes builtin (pi en el calculo de alpha/k), matriz sparse + backslash,
%   return-map con ramas, matriz literal, y acumulacion por indice Fint(d).
%
% Malla y materiales EXACTOS del InputFile de GEO5 Demo04 (628 nodos, 287 T6).
% Referencias verificadas contra MATLAB 2017a y Abaqus 2017:
%   |uel|max (gravedad+sobrecarga, elastico) = 22.690 mm
%   SRF=1.00 -> it=13, |u|max=0.316 mm, res=8.70e-04
%   SRF=1.40 -> it=60, |u|max=2.369 mm
% El campo elastico coincide con Abaqus a 1 nm (ver hekatan-geo5-bridge).
%
% NOTA: usa sig0 APLANADO (4 x ne*3) a proposito, para no depender del camino
% 3-D. Asi, si este test falla, el problema NO esta en los arreglos 3-D (que
% tienen su propio canario en t2).

[X,Y,ELE,EMAT]=demo04_mesh();
nn=numel(X); ne=size(ELE,1); ndof=2*nn;
MAT=[1.303470e5 0.30 22.70 9.0 18.0; 1.303470e5 0.20 38.00 120.0 20.0];

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
D3=cell(2,1); D4=cell(2,1);
for mm=1:2; E=MAT(mm,1); nu=MAT(mm,2); f=E/((1+nu)*(1-2*nu));
  D3{mm}=f*[1-nu nu 0; nu 1-nu 0; 0 0 (1-2*nu)/2];
  D4{mm}=f*[1-nu nu nu 0; nu 1-nu nu 0; nu nu 1-nu 0; 0 0 0 (1-2*nu)/2]; end

F0=zeros(ndof,1);
for e=1:ne; g=MAT(EMAT(e),5); nd=ELE(e,:); xy=[X(nd) Y(nd)];
  for q=1:3; [Nv,dL1,dL2]=t6shp(gp(q,1),gp(q,2)); J=[dL1'*xy(:,1) dL1'*xy(:,2); dL2'*xy(:,1) dL2'*xy(:,2)]; w=det(J)*gw(q);
    for a=1:6; F0(2*nd(a))=F0(2*nd(a))-Nv(a)*g*w; end; end; end
qsur=-35.0; sf={[28 27 238],[30 28 241]};
for s=1:2; ed=sf{s}; L=abs(X(ed(2))-X(ed(1)));
  F0(2*ed(1))=F0(2*ed(1))+qsur*L/6; F0(2*ed(2))=F0(2*ed(2))+qsur*L/6; F0(2*ed(3))=F0(2*ed(3))+qsur*2*L/3; end

% --- comprobaciones del ensamblaje (detectan jacobiano transpuesto y similares) ---
disp(['CHECK area ' num2str(sum(dJw(:)),12)]);
disp(['CHECK peso ' num2str(-sum(F0(2:2:end)),12)]);

K=sparse(ndof,ndof);
for e=1:ne; Dm=D3{EMAT(e)}; Ke=zeros(12,12); for q=1:3; B=Bc{e,q}; Ke=Ke+B'*Dm*B*dJw(e,q); end; d=edof(e,:); K(d,d)=K(d,d)+Ke; end
Kff=K(free,free);
uel=zeros(ndof,1); uel(free)=Kff\F0(free);
disp(['CHECK uel_max ' num2str(max(sqrt(uel(1:2:end).^2+uel(2:2:end).^2))*1000,12)]);

% consistencia: Fint(sig0) debe reproducir F0 (residuo ~1e-13)
sig0=zeros(4,ne*3);
for e=1:ne; D=D4{EMAT(e)}; d=edof(e,:);
  for q=1:3; ep=Bc{e,q}*uel(d'); sig0(:,(e-1)*3+q)=D*[ep(1);ep(2);0;ep(3)]; end; end
big=[1e9;1e9];
Fi0=fint_c(zeros(ndof,1),ne,EMAT,D4,big,big,sig0,Bc,dJw,edof,ndof);
disp(['CHECK residuo_inicial ' num2str(norm(F0(free)-Fi0(free))/norm(F0(free)),12)]);

% --- SRM en dos puntos ---
for SRF=[1.00 1.40]
  alp=zeros(2,1); kc=zeros(2,1);
  for mm=1:2; ph=atan(tan(MAT(mm,3)*pi/180)/SRF); c=MAT(mm,4)/SRF; sp=sin(ph); cp=cos(ph);
    alp(mm)=2*sp/(sqrt(3)*(3+sp)); kc(mm)=6*c*cp/(sqrt(3)*(3+sp)); end
  u=zeros(ndof,1); conv=0; rn=1; nF0=max(norm(F0(free)),1); it=0;
  for it=1:400
    Fint=fint_c(u,ne,EMAT,D4,alp,kc,sig0,Bc,dJw,edof,ndof);
    R=F0-Fint; rn=norm(R(free))/nF0;
    if it>1 && rn<1e-3; conv=1; break; end
    du=zeros(ndof,1); du(free)=Kff\R(free);
    b=1; br=inf;
    for et=[1 0.7 0.4 0.2 0.1]
      F2=fint_c(u+et*du,ne,EMAT,D4,alp,kc,sig0,Bc,dJw,edof,ndof);
      r2=norm(F0(free)-F2(free)); if r2<br; br=r2; b=et; end
    end
    u=u+b*du;
    if any(~isfinite(u)) || max(abs(u))>2; break; end
  end
  tag=num2str(round(SRF*100));
  disp(['CHECK srf' tag '_it ' num2str(it)]);
  disp(['CHECK srf' tag '_conv ' num2str(conv)]);
  disp(['CHECK srf' tag '_umax ' num2str(max(sqrt(u(1:2:end).^2+u(2:2:end).^2))*1000,12)]);
end
