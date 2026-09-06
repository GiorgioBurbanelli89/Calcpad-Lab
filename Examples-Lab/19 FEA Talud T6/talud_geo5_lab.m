function talud_geo5_lab(nstages,tag)
% Talud Demo04 de GEO5 (GeoFEM): PORT FIEL a MATLAB 2017a / Hekatan Lab del driver Python
% talud_python_3etapas/talud_geo5mesh.py (el que cierra FS etapa 1 = 1.6935 = GEO5 exacto).
%
% Todo lo que hay aqui esta EXTRAIDO del solver de GEO5 (FRGeoFEM.exe) o MEDIDO en su course of
% analysis, nada es de libro:
%   - malla, apoyos, cargas y materiales: demo04_geo5_fixture.m (parseado del InputFile.txt)
%   - E = 130347 kPa en los DOS suelos (campo E= del InputFile; medido: |ui|/|u| it=1 = 0.33398)
%   - sigma inicial = 0 y cada peldano arranca de cero (initial_fields vacio; it=1 identica)
%   - cuadratura de 7 puntos (constructor de T6s_PlaneStrain, +0x28 = 7)
%   - Drucker-Prager ajuste de EXTENSION: alpha = 2 sen(phi) / (sqrt3 (3+sen phi))  (FUN_005b9e60)
%   - return-map: Newton local implicito = retorno radial (psi=0), tangente ALGORITMICA no simetrica,
%     apice -> De  (FUN_005bf160 / FUN_005c0ce0 / FUN_005bfce0)
%   - reduccion: phi_red = atan(tan phi / SRF), c_red = c/SRF  (capturado en vivo: "Current phi")
%   - Newton COMPLETO (tangente cada iteracion), line-search secante de FUN_00510a90 (tol 0.8,
%     eta en [0.1, 1.0]) y las TRES normas de FUN_005eb0a0 con num/max(den,1) (ABSOLUTAS bajo 1)
%   - escalera SRM: paso 0.90 multiplicativo, relajacion /2, max 3, tope 0.99
%
% Uso:  talud_geo5_lab        (3 etapas)      talud_geo5_lab(1)   (solo etapa 1)
% Imprime el mismo log por iteracion que el Python (RS, SRF, it, eta, 3 normas, |gi|, dx) para
% comparar numero a numero contra geo5mesh_iterlog_dp.txt.
if nargin<1; nstages=3; end
if nargin<2; tag='m'; end      % prefijo de los PNG: 'm' = MATLAB, 'lab' = Hekatan Lab (no pisarse)
ttotal=tic;
[X,Y,ELE,EMAT,FIXED,Fg,Fs,Fa,MAT]=demo04_geo5_fixture();
nn=numel(X); ne=size(ELE,1); ndof=2*nn;
free=setdiff((1:ndof)',FIXED);
fprintf('MALLA GEO5: %d nodos, %d T6 (SOIL_1=%d, SOIL_2=%d), %d gdl fijos\n',nn,ne,sum(EMAT==1),sum(EMAT==2),numel(FIXED));
for mm=1:2
  fprintf('MAT SOIL_%d: E=%.0f nu=%.2f phi=%.2f c=%.2f gamma=%.1f psi=%.1f\n',mm,MAT(mm,1),MAT(mm,2),MAT(mm,3),MAT(mm,4),MAT(mm,5),MAT(mm,6));
end
% ---- cuadratura de 7 puntos (Hammer grado 5), pesos /2 = area del triangulo de referencia ----
c7=0.1012865073235; C7=0.7974269853531; e7=0.4701420641051; E7=0.0597158717898;
gp=[1/3 1/3; c7 c7; C7 c7; c7 C7; e7 e7; E7 e7; e7 E7];
gw=[0.225 0.1259391805448 0.1259391805448 0.1259391805448 0.1323941527885 0.1323941527885 0.1323941527885]/2;
NG=7;
% ---- constitutivo elastico por suelo (plano-deformacion, 4 comp: xx yy zz xy) ----
D4=cell(2,1);
for mm=1:2
  E=MAT(mm,1); nu=MAT(mm,2); f=E/((1+nu)*(1-2*nu));
  D4{mm}=f*[1-nu nu nu 0; nu 1-nu nu 0; nu nu 1-nu 0; 0 0 0 (1-2*nu)/2];
end
IX=[1 2 4];                                   % bloque en-plano (xx, yy, xy)
% ---- B, det(J)*w y gdl por elemento ----
Bc=cell(ne,NG); dJw=zeros(ne,NG); edof=zeros(ne,12);
for e=1:ne
  nd=ELE(e,:); xy=[X(nd) Y(nd)];
  for a=1:6; edof(e,2*a-1)=2*nd(a)-1; edof(e,2*a)=2*nd(a); end
  for q=1:NG
    [~,dL1,dL2]=t6shape(gp(q,1),gp(q,2));
    J=[dL1'*xy(:,1) dL1'*xy(:,2); dL2'*xy(:,1) dL2'*xy(:,2)]; dJ=det(J);
    dNx=( J(2,2)*dL1-J(1,2)*dL2)/dJ; dNy=(-J(2,1)*dL1+J(1,1)*dL2)/dJ;
    B=zeros(3,12);
    for a=1:6; B(1,2*a-1)=dNx(a); B(2,2*a)=dNy(a); B(3,2*a-1)=dNy(a); B(3,2*a)=dNx(a); end
    Bc{e,q}=B; dJw(e,q)=dJ*gw(q);
  end
end
% ---- autochequeo: la gravedad consistente recalculada aqui debe ser la de la fixture ----
Fg2=zeros(ndof,1);
for e=1:ne
  rho=-MAT(EMAT(e),5); nd=ELE(e,:);
  for q=1:NG; Nv=t6shape(gp(q,1),gp(q,2)); for a=1:6; Fg2(2*nd(a))=Fg2(2*nd(a))+Nv(a)*rho*dJw(e,q); end; end
end
fprintf('cargas: gravedad Fy=%.3f (recalculada %.3f, dif max %.2e) | sobrecarga Fy=%.3f | ancla Fx=%.3f Fy=%.3f kN\n',...
  sum(Fg(2:2:end)),sum(Fg2(2:2:end)),max(abs(Fg2-Fg)),sum(Fs(2:2:end)),sum(Fa(1:2:end)),sum(Fa(2:2:end)));
% ---- K elastica (respaldo si la tangente sale singular) ----
[II,JJ,VV]=deal(zeros(144*ne,1)); p=0;
for e=1:ne
  De=D4{EMAT(e)}; Dm=De(IX,IX); Ke=zeros(12,12);
  for q=1:NG; B=Bc{e,q}; Ke=Ke+B'*Dm*B*dJw(e,q); end
  d=edof(e,:); [rr,cc]=ndgrid(d,d); ii=p+(1:144); II(ii)=rr(:); JJ(ii)=cc(:); VV(ii)=Ke(:); p=p+144;
end
Kel=sparse(II,JJ,VV,ndof,ndof); Kel=Kel(free,free);
% ---- etapas ----
names={'etapa1 (peso propio)','etapa2 (+sobrecarga)','etapa3 (+ancla)'};
Fst={Fg, Fg+Fs, Fg+Fs+Fa}; gfs=[1.69 1.48 1.69];
FS=zeros(1,nstages); TT=zeros(1,nstages);
T_asm=0; T_tan=0; T_sol=0; N_asm=0; N_sol=0;   % perfil por fases (tic/toc), ANTES del bucle (lo leen las anidadas)
for si=1:nstages
  tst=tic;
  fprintf('\n### %s ###\n',names{si});
  [fs,prog,ulast,uel]=srm(Fst{si});
  ulast=ulast-uel;   % campo que tabula/dibuja GEO5: u(FS) - u_ELASTICA (medido, 2026-09-03)
  FS(si)=fs; TT(si)=toc(tst);
  fprintf('    progresion dx(mm) por SRF: %s\n',prog);
  fprintf('  %-22s >>> FS=%.4f  (GEO5=%.2f)  [%.1f s]\n',names{si},fs,gfs(si),TT(si));
  fprintf('    PERFIL: ensamblajes %d en %.2f s (%.1f ms/u) | tangentes %.2f s | resoluciones %d en %.2f s (%.1f ms/u) | resto %.2f s\n',N_asm,T_asm,1e3*T_asm/max(N_asm,1),T_tan,N_sol,T_sol,1e3*T_sol/max(N_sol,1),TT(si)-T_asm-T_tan-T_sol);
  T_asm=0; T_tan=0; T_sol=0; N_asm=0; N_sol=0;
  % GRAFICA: campo d_x [mm] del ultimo peldano convergido (el que GEO5 tabula), 12 bandas con la
  % paleta de GEO5, isolineas, FS en el titulo. Mismo dibujo que talud_geo5_plot.py (matplotlib).
  try; dlmwrite(sprintf('talud_u_%s_e%d.txt',tag,si),ulast,'precision',15); catch; end   % para redibujar sin recalcular
  plot_talud_stage(X,Y,ELE,EMAT,Fs,Fa,MAT,ulast,fs,gfs(si),names{si},sprintf('talud_%s_e%d.png',tag,si),si);
  plot_talud_stage(X,Y,ELE,EMAT,Fs,Fa,MAT,ulast,fs,gfs(si),names{si},sprintf('talud_%s_e%d_dz.png',tag,si),si,'dz');   % d_z y resultante (2026-09-04)
  plot_talud_stage(X,Y,ELE,EMAT,Fs,Fa,MAT,ulast,fs,gfs(si),names{si},sprintf('talud_%s_e%d_d.png',tag,si),si,'d');
end
fprintf('\n================ RESUMEN (Hekatan Lab / MATLAB, malla exacta GEO5) ================\n');
for si=1:nstages; fprintf('  %-22s FS=%.4f  (GEO5 %.2f)  dif=%+.1f%%  t=%.1f s\n',names{si},FS(si),gfs(si),100*(FS(si)/gfs(si)-1),TT(si)); end
fprintf('TOTAL %.1f s\n',toc(ttotal));

% ================= SRM: escalera geometrica de GEO5 ('Analysis settings') =================
SIG=[]; DEPG=[]; HASDEP=[]; T_asm=0; T_tan=0; T_sol=0; N_asm=0; N_sol=0;   % perfil por fases (tic/toc)   % estado por punto de Gauss, declarado AQUI para que lo compartan las funciones anidadas

function [fs,prog,ulast,uel]=srm(Ftot)
  uel=zeros(ndof,1); uel(free)=Kel\Ftot(free);   % referencia de GEO5: solucion elastica
  RED0=0.90; RELAX=2.0; MINSTEP=0.99; MAXRELAX=3;
  Racc=1.0; fs=1.0; nrelax=0; rs=0; prog='';
  ustart=zeros(ndof,1); ulast=ustart;      % sigma0 = 0 y cada peldano desde cero (medido en GEO5)
  while true
    s=1.0-(1.0-RED0)/(RELAX^nrelax);       % 0.90 -> 0.95 -> 0.975 -> 0.9875
    if s>MINSTEP; break; end
    trial=1.0/(Racc*s);   % SRF EXACTO (2026-09-04: GEO5 no redondea; con round(.,4) la etapa 1 se iba de 1e-12 a 1e-5 en el primer peldano)
    if trial>3.0; break; end
    rs=rs+1;
    [conv,u,nit]=nrstep(trial,Ftot,ustart,rs);
    if conv && all(isfinite(u))
      Racc=Racc*s; fs=trial; ulast=u;   % GEO5 NO reinicia la relajacion tras converger
      prog=[prog sprintf(' %.3f:%.0f',trial,max(abs(u(1:2:end)))*1e3)];
      fprintf('    SRM rs%02d paso=%.4f SRF=%.4f CONVERGE it=%d\n',rs,s,trial,nit);
    else
      nrelax=nrelax+1;
      fprintf('    SRM rs%02d paso=%.4f SRF=%.4f DIVERGE relax=%d\n',rs,s,trial,nrelax);
      if nrelax>MAXRELAX; break; end
    end
  end
end

% ================= un reduction step: Newton completo + line-search + 3 normas del binario =====
function [conv,u,it]=nrstep(SRF,Fext,u0,rstep)
  % ALGEBRA DE CADA ITERACION DE GEO5 (medida en g5_1e y en el talud entero, iteracion a
  % iteracion a 4 cifras, 2026-09-03):
  %  1) du = Kt\R con Kt armada con la tangente ALGORITMICA del ULTIMO retorno de cada punto de Gauss;
  %  2) line-search de un tiro evaluando R(u+du) con un retorno de PRUEBA desde el estado;
  %  3) u = u + eta du y la tension se ACUMULA: sigma_i = retorno(sigma_{i-1} + De B eta du);
  %  4) residuo con sigma_i y las tres normas. Cada reduction step arranca de cero.
  maxit=100;
  ab=zeros(2,2);
  for mm=1:2
    phi=atan(tan(MAT(mm,3)*pi/180)/SRF); c=MAT(mm,4)/SRF;   % ley de reduccion medida en vivo
    [ab(mm,1),ab(mm,2)]=dp_ab(phi,c);
  end
  u=u0; conv=false; it=0;
  SIG=zeros(4,ne*NG); DEPG=zeros(4,4,ne*NG); HASDEP=false(1,ne*NG);   % estado por punto de Gauss
  Fi=assemble_inc(zeros(ndof,1),ab,false); Rf=Fext(free)-Fi(free);
  ADDisp=zeros(numel(free),1); DForce=Rf; CLoad=Rf; r_prev=Inf; ndiv=0;
  for it=1:maxit
    if ~all(isfinite(Rf)); break; end
    t1=tic; Kt=tangent_from_state(ab); T_tan=T_tan+toc(t1);   % tangente del ULTIMO retorno (De si no lo hubo)
    t1=tic; duf=Kt\Rf; T_sol=T_sol+toc(t1); N_sol=N_sol+1;
    if ~all(isfinite(duf)); duf=Kel\Rf; end           % K tangente singular -> K elastica (como el Python)
    du=zeros(ndof,1); du(free)=duf;
    % line-search secante de FUN_00510a90: compara NORMAS de residuo, una sola pasada
    s0=duf'*Rf; n0=norm(Rf); al=1.0;
    t1=tic; F1=assemble_inc(du,ab,false); T_asm=T_asm+toc(t1); N_asm=N_asm+1; R1=Fext(free)-F1(free);   % retorno de PRUEBA desde el estado
    s1=duf'*R1; n1=norm(R1); den=s0-s1;
    if n0>1e-10 && n1>1e-10 && abs(den)>1e-10 && n1/n0>=0.8; al=al*s0/den; end
    if al<0.1; al=0.1; elseif al>=1.0; al=1.0; end
    drf=al*duf; u=u+al*du;
    t1=tic; Fi=assemble_inc(al*du,ab,true); T_asm=T_asm+toc(t1); N_asm=N_asm+1; Rf=Fext(free)-Fi(free); % COMMIT: sigma_i y su tangente
    % las tres normas de FUN_005eb0a0: v = num / max(den, 1)
    ADDisp=ADDisp+drf;
    nDD=norm(drf); dA=norm(ADDisp); nDL=norm(Rf); dF=norm(DForce);
    nEN=sqrt(abs(drf'*Rf)); dE=sqrt(abs(ADDisp'*DForce));   % E con el residuo DESPUES de actualizar (medido en el Log de GEO5)
    if dA>1.0; eu=nDD/dA; else; eu=nDD; end
    if dF>1.0; ef=nDL/dF; else; ef=nDL; end
    if dE>1.0; ee=nEN/dE; else; ee=nEN; end
    CLoad=Rf;
    fprintf('  RS=%d SRF=%.4f it=%2d eta=%.4f DNorm=%.5e OBFNorm=%.5e ENorm=%.5e |gi|=%.4e dx=%.1f\n',...
      rstep,SRF,it,al,eu,ef,ee,nDL,max(abs(u(1:2:end)))*1e3);
    if ef<1e-2 && ee<1e-2 && eu<1e-2; conv=true; break; end
    if ef>250.0; break; end                            % aborto duro del binario (estado[0x354]=8)
    if nDL<=r_prev || ef<=1e-2; ndiv=0; else; ndiv=ndiv+1; end
    r_prev=nDL;
    if ndiv>=2; break; end                             % MEDIDO en los Log de GEO5: divergencia tras DOS subidas seguidas de |gi|                             % divergencia: el residuo crudo sube 3 veces
  end
end

% ===== ensamblaje incremental (GEO5): sigma = retorno(SIG + De B du) por punto de Gauss =====
function Fi=assemble_inc(du,ab,commit)
  Fi=zeros(ndof,1);
  for e=1:ne
    mm=EMAT(e); De=D4{mm}; d=edof(e,:); ue=du(d); al=ab(mm,1); k=ab(mm,2);
    for q=1:NG
      kk=(e-1)*NG+q; B=Bc{e,q}; ep=B*ue;
      [sg,Dep]=dp_return_g5([ep(1);ep(2);0;ep(3)],al,k,De,commit,SIG(:,kk));
      Fi(d)=Fi(d)+B'*[sg(1);sg(2);sg(4)]*dJw(e,q);
      if commit; SIG(:,kk)=sg; DEPG(:,:,kk)=Dep; HASDEP(kk)=true; end
    end
  end
end

% ===== tangente con la del ULTIMO retorno guardado (De donde aun no hubo retorno) =====
function Kt=tangent_from_state(ab)
  I2=zeros(144*ne,1); J2=I2; V2=I2; p2=0;
  for e=1:ne
    mm=EMAT(e); De=D4{mm}; d=edof(e,:); Ke=zeros(12,12);
    for q=1:NG
      kk=(e-1)*NG+q; B=Bc{e,q};
      if HASDEP(kk); Dep=DEPG(:,:,kk); else; Dep=De; end
      Ke=Ke+B'*Dep(IX,IX)*B*dJw(e,q);
    end
    [rr,cc]=ndgrid(d,d); ii=p2+(1:144); I2(ii)=rr(:); J2(ii)=cc(:); V2(ii)=Ke(:); p2=p2+144;
  end
  Kt=sparse(I2,J2,V2,ndof,ndof); Kt=Kt(free,free);
end

% ================= (modo antiguo) retorno unico desde la deformacion TOTAL, sin estado =============
function [Fi,Kt]=assemble(u,ab,wantK)
  Fi=zeros(ndof,1); Kt=[];
  if wantK; I2=zeros(144*ne,1); J2=I2; V2=I2; p2=0; end
  for e=1:ne
    mm=EMAT(e); De=D4{mm}; d=edof(e,:); ue=u(d); al=ab(mm,1); k=ab(mm,2);
    if wantK; Ke=zeros(12,12); end
    for q=1:NG
      B=Bc{e,q}; ep=B*ue;
      [sg,Dep]=dp_return_g5([ep(1);ep(2);0;ep(3)],al,k,De,wantK);
      Fi(d)=Fi(d)+B'*[sg(1);sg(2);sg(4)]*dJw(e,q);
      if wantK; Ke=Ke+B'*Dep(IX,IX)*B*dJw(e,q); end
    end
    if wantK; [rr,cc]=ndgrid(d,d); ii=p2+(1:144); I2(ii)=rr(:); J2(ii)=cc(:); V2(ii)=Ke(:); p2=p2+144; end
  end
  if wantK; Kt=sparse(I2,J2,V2,ndof,ndof); Kt=Kt(free,free); end
end
end

% ---- Drucker-Prager, ajuste de EXTENSION (FUN_005b9e60 flag 2): alpha, k desde phi(rad), c ----
function [al,k]=dp_ab(phi,c)
s=sin(phi); co=cos(phi); r3=sqrt(3.0);
al=2*s/(r3*(3+s)); k=6*c*co/(r3*(3+s));
end

% ---- return-map D-P de GEO5 (sigma_n = 0: sin historia, plasticidad perfecta, psi=0) ----
% f = sqrt(J2) + alpha I1 - k.  Cono: lambda = f_tr/G, beta = J/J_tr, sigma = sm*1 + beta*s_tr.
% Tangente ALGORITMICA no simetrica: Xi = K 1 1' + beta 2G Pdev (Xi44 = beta G),
%   Dep = Xi - (Xi m)(n' Xi)/(n' Xi m),  n = w + alpha 1,  m = w (psi=0),  w = d(sqrtJ2)/dsigma.
% Apice: sigma = p_apex 1 y la tangente se deja ELASTICA (FUN_005bfce0, flag 0x10).
function [sig,Dep]=dp_return_g5(deps,al,k,De,wantK,sig_n)
ONE=[1;1;1;0]; Dep=De;
G=De(4,4); K=(De(1,1)+2*De(1,2))/3;
sig=De*deps;
if nargin>5; sig=sig+sig_n; end                % estado acumulado de la iteracion anterior (GEO5)
I1=sig(1)+sig(2)+sig(3); sm=I1/3; s=sig-sm*ONE;
J=sqrt(max(0.5*(s(1)^2+s(2)^2+s(3)^2)+s(4)^2,0));
f=J+al*I1-k;
if f<=1e-12; return; end                       % elastico
if al>1e-12; apex_p=k/(3*al); else; apex_p=Inf; end
if sm<apex_p && J>1e-12
  lam=f/G; Jn=J-G*lam;
  if Jn>=1e-12
    beta=Jn/J; sig=sm*ONE+beta*s;
    if ~wantK; return; end
    p=(sig(1)+sig(2)+sig(3))/3; dv=[sig(1)-p;sig(2)-p;sig(3)-p;sig(4)];
    sj=sqrt(max(0.5*(dv(1)^2+dv(2)^2+dv(3)^2)+dv(4)^2,0));
    if sj<1e-12; Dep=1e-3*De; return; end
    w=[dv(1);dv(2);dv(3);2*dv(4)]/(2*sj); n=w+al*ONE; m=w;
    PDEV=eye(4)-(ONE*ONE')/3;
    Xi=K*(ONE*ONE')+beta*(2*G*PDEV); Xi(4,4)=beta*G;
    Xm=Xi*m; den=n'*Xm;
    if abs(den)>1e-30; Dep=Xi-(Xm*(n'*Xi))/den; else; Dep=Xi; end
    return;
  end
end
sig=[apex_p;apex_p;apex_p;0];                  % apice, tangente = De
end

% ---- funciones de forma T6 ----
function [Nv,dNdL1,dNdL2]=t6shape(L1,L2)
L3=1-L1-L2;
Nv=[L1*(2*L1-1); L2*(2*L2-1); L3*(2*L3-1); 4*L1*L2; 4*L2*L3; 4*L3*L1];
dNdL1=[4*L1-1;0;-(4*L3-1);4*L2;-4*L2;4*L3-4*L1];
dNdL2=[0;4*L2-1;-(4*L3-1);4*L1;4*L3-4*L2;-4*L1];
end
