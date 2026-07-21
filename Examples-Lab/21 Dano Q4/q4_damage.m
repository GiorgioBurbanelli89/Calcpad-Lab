function q4_damage
%% Q4 con DANO (Concrete Damaged Plasticity) - Abaqus vs Hekatan Lab
% Corre IGUAL en MATLAB 2017a y en Hekatan Lab.
%
% PARTE A: UN elemento Q4 (= CPS4 de Abaqus) en traccion uniaxial. Mismas
%          coordenadas y mismas condiciones de borde que ET_ten.inp, asi la
%          comparacion contra ET_ten_se.csv es 1:1.
% PARTE B: panel de Q4 con ENTALLA. La grieta arranca en la entalla y
%          propaga: el dano LOCALIZA en una banda.
%
% Material tal cual ET_ten.inp:
%   E=23500 MPa, nu=0.2, ft=2.5, fc=25
%   CDP: psi=38, ecc=0.1, fb0/fc0=1.16, Kc=0.667
%   La curva de traccion del .inp (2.5 -> 0.125) es la tension NOMINAL:
%   la EFECTIVA se queda en ft y la caida la produce el dano, porque
%   2.5*(1-0.95) = 0.125.
clc; close all;
global M; M = params();

parte_A();
parte_B();
end

%% ------------------------------------------------------------- parametros --
function m = params()
m.E = 23500; m.nu = 0.2; m.ft = 2.5; m.fc = 25;
fb = 1.16; Kc = 0.667;
m.alpha = (fb-1)/(2*fb-1);
m.gamma = 3*(1-Kc)/(2*Kc-1);
m.psi = 38*pi/180; m.ecc = 0.1;
m.wt = 0.0; m.wc = 1.0;      % recuperacion rigidez: traccion no, compresion si
m.D0 = m.E/(1-m.nu^2)*[1 m.nu 0; m.nu 1 0; 0 0 (1-m.nu)/2];
% puntos de Gauss 2x2
g = 1/sqrt(3);
m.GP = [-g -g; g -g; g g; -g g];
end

function s = sbar_t(~)
global M; s = M.ft;            % traccion efectiva perfectamente plastica
end
function s = sbar_c(kc)
global M;
s = interp1([0 0.0018 1],[0.5*M.fc M.fc M.fc], min(max(kc,0),1), 'linear', M.fc);
end
function d = dano_t(kt)
% Abaqus tabula el dano a traccion vs CRACKING STRAIN (0.95 a eps_ck=0.0008 en el
% .inp), NO vs la deformacion plastica. Conversion exacta de Abaqus (del kernel
% ABQSMAStaCore, ver hekatan-abaqus-bridge):
%   eps_ck = eps_pl + dt*sigma_eff/E     con sigma_eff = ft (traccion perf. plastica)
% dt depende de eps_ck -> punto fijo (converge en 3-4 iteraciones). Asi se usa el
% valor LITERAL 0.0008 del .inp, sin umbral calibrado. RMS vs Abaqus = 0.35%.
global M;
ect = kt; d = 0;
for it = 1:5
    d = interp1([0 0.0008],[0 0.95], min(max(ect,0),0.0008), 'linear', 0.95);
    ect = kt + d*M.ft/M.E;
end
end
function d = dano_c(kc)
d = interp1([0 0.0018 0.004 0.02],[0 0 0.5 0.7], min(max(kc,0),0.02),'linear',0.7);
end

%% -------------------------------------------------- superficie de Lubliner --
function [p1,p2] = ppales(s)
mm = 0.5*(s(1)+s(2));
rr = sqrt((0.5*(s(1)-s(2)))^2 + s(3)^2);
p1 = mm+rr; p2 = mm-rr;
end

function F = yieldF(sb,kt,kc)
global M; a = M.alpha;
I1 = sb(1)+sb(2); p = -I1/3;
sxx = sb(1)-I1/3; syy = sb(2)-I1/3; szz = -I1/3;
J2 = 0.5*(sxx^2+syy^2+szz^2) + sb(3)^2;
q = sqrt(3*max(J2,0));
[p1,~] = ppales(sb); smax = max(p1,0);
st = sbar_t(kt); sc = sbar_c(kc);
beta = (sc/max(st,1e-6))*(1-a) - (1+a);
F = ( q - 3*a*p + beta*max(smax,0) - M.gamma*max(-smax,0) )/(1-a) - sc;
end

function g = gradF(sb,kt,kc)
g = zeros(3,1); h = 1e-4*(1+norm(sb));
for k = 1:3
    e = zeros(3,1); e(k) = h;
    g(k) = (yieldF(sb+e,kt,kc) - yieldF(sb-e,kt,kc))/(2*h);
end
end

function n = flujo(sb)
% potencial Drucker-Prager hiperbolico -> flujo NO asociado (dilatancia)
global M;
I1 = sb(1)+sb(2); sxx = sb(1)-I1/3; syy = sb(2)-I1/3;
J2 = 0.5*(sxx^2+syy^2+(-I1/3)^2) + sb(3)^2;
q = sqrt(3*max(J2,1e-12));
tanp = tan(M.psi);
den = sqrt((M.ecc*M.ft*tanp)^2 + q^2);
dq = 1.5/q*[sxx; syy; 2*sb(3)];
dp = -[1;1;0]/3;
n = (q/den)*dq - tanp*dp;
if norm(n) > 1e-9, n = n/norm(n); end
end

function r = peso(sb)
[p1,p2] = ppales(sb);
pp = [p1 p2 0]; s = sum(abs(pp));
if s < 1e-9, r = 0.5; else r = sum(max(pp,0))/s; end
end

%% ------------------------------------------- un paso constitutivo del CDP --
function [sig,epl,kt,kc,dt,dc] = cdp_paso(eps,epl,kt,kc)
global M; D0 = M.D0;
sb = D0*(eps-epl);
F = yieldF(sb,kt,kc);
it = 0;
while F > 1e-5 && it < 60
    it = it+1;
    n  = flujo(sb);
    nF = gradF(sb,kt,kc);
    H  = max((sbar_c(kc+1e-6)-sbar_c(kc))/1e-6*0.3, 0);
    den = nF.'*D0*n + H;
    if abs(den) < 1e-9, break; end
    dl = F/den; if dl < 0, dl = 0; end
    depl = dl*n; epl = epl + depl;
    [e1,e2] = ppales([depl(1); depl(2); 0.5*depl(3)]);
    r = peso(sb);
    kt = kt + r*max(e1,0);
    kc = kc + (1-r)*max(-e2,0);
    sb = D0*(eps-epl);
    F = yieldF(sb,kt,kc);
end
dt = dano_t(kt); dc = dano_c(kc);
% efecto unilateral (Lee-Fenves): en compresion la grieta de traccion CIERRA
[p1,p2] = ppales(sb);
sabs = abs(p1)+abs(p2);
if sabs < 1e-9, rst = 0.5; else rst = (max(p1,0)+max(p2,0))/sabs; end
sT = 1-M.wt*rst; sC = 1-M.wc*(1-rst);
d = 1-(1-sT*dc)*(1-sC*dt);
sig = (1-d)*sb;
end

%% ----------------------------------------------------------------- Q4 FEM --
function [B,detJ] = q4_B(xy,xi,eta)
dN = 0.25*[-(1-eta)  (1-eta)  (1+eta) -(1+eta);
           -(1-xi)  -(1+xi)   (1+xi)   (1-xi)];
J = dN*xy;
detJ = det(J);
dNxy = J\dN;
B = zeros(3,8);
B(1,1:2:7) = dNxy(1,:);
B(2,2:2:8) = dNxy(2,:);
B(3,1:2:7) = dNxy(2,:);
B(3,2:2:8) = dNxy(1,:);
end

function [uh,Rh,dth,dte] = fem_q4(nodos,elems,fijos,gimp,vimp,npasos,esp)
% FEM Q4 tension plana con CDP, control por DESPLAZAMIENTO.
% Newton MODIFICADO (rigidez elastica): con ablandamiento la tangente se
% vuelve negativa y el Newton completo diverge.
global M;
nn = size(nodos,1); ne = size(elems,1); ndof = 2*nn;
ng = size(M.GP,1);
% estado por punto de Gauss en ARREGLOS planos (sin cell arrays: Lab y MATLAB
% se comportan igual asi)
EPL = zeros(3, ne*ng); KT = zeros(1, ne*ng); KC = zeros(1, ne*ng);

libre = true(1,ndof);
libre(fijos) = false;
libre(gimp)  = false;
idl = find(libre);

% grados de libertad de cada elemento
GDL = zeros(ne,8);
for e = 1:ne
    c = elems(e,:);
    GDL(e,:) = [2*c(1)-1 2*c(1) 2*c(2)-1 2*c(2) 2*c(3)-1 2*c(3) 2*c(4)-1 2*c(4)];
end

U = zeros(ndof,1);
uh = zeros(npasos,1); Rh = zeros(npasos,1); dth = zeros(npasos,1);
dte = zeros(ne,1);
for paso = 1:npasos
    f = paso/npasos;
    U(gimp) = f*vimp;

    for iter = 1:60
        Fint = zeros(ndof,1);
        K = zeros(ndof,ndof);
        for e = 1:ne
            xy = nodos(elems(e,:),:);
            gd = GDL(e,:); ue = U(gd);
            fe = zeros(8,1); ke = zeros(8,8);
            for ig = 1:ng
                [B,dJ] = q4_B(xy,M.GP(ig,1),M.GP(ig,2));
                k = (e-1)*ng+ig;
                sig = cdp_paso(B*ue, EPL(:,k), KT(k), KC(k));
                w = dJ*esp;
                fe = fe + B.'*sig*w;
                ke = ke + B.'*M.D0*B*w;
            end
            Fint(gd) = Fint(gd) + fe;
            K(gd,gd) = K(gd,gd) + ke;
        end
        R = -Fint(idl);
        if norm(R) < 1e-6*max(1,norm(Fint)), break; end
        U(idl) = U(idl) + K(idl,idl)\R;
    end

    % convergido: AHORA se actualiza el estado (plasticidad y dano)
    Fint = zeros(ndof,1);
    for e = 1:ne
        xy = nodos(elems(e,:),:);
        gd = GDL(e,:); ue = U(gd);
        ac = 0;
        for ig = 1:ng
            [B,dJ] = q4_B(xy,M.GP(ig,1),M.GP(ig,2));
            k = (e-1)*ng+ig;
            [sig,epl,kt,kc,dt] = cdp_paso(B*ue, EPL(:,k), KT(k), KC(k));
            EPL(:,k) = epl; KT(k) = kt; KC(k) = kc;
            Fint(gd) = Fint(gd) + B.'*sig*(dJ*esp);
            ac = ac + dt;
        end
        dte(e) = ac/ng;
    end
    uh(paso) = U(gimp(1));
    Rh(paso) = sum(Fint(gimp));
    dth(paso) = mean(dte);
end
end

%% ------------------------------------------ PARTE A: un solo elemento Q4 ---
function parte_A()
fprintf('==================================================================\n');
fprintf('PARTE A - UN elemento Q4 (CPS4) en traccion, contra Abaqus\n');
fprintf('==================================================================\n');
tic;
% coordenadas y BCs EXACTAS de ET_ten.inp
nodos = [0 0; 1 0; 1 1; 0 1];
elems = [1 2 3 4];
fijos = [1 2 7 4];          % n1:ux,uy   n4:ux   n2:uy
gimp  = [3 5];              % n2 y n3: ux impuesto
ref = load('ET_ten_se.csv');
np = size(ref,1)-1;        % misma resolucion que Abaqus (comparacion 1:1)
vimp = 0.0012;
[uh,Rh,dth] = fem_q4(nodos,elems,fijos,gimp,vimp,np,1.0);
t = toc;

% elemento de lado 1 -> deformacion = desplazamiento, tension = reaccion
sig_ref = interp1(ref(:,1),ref(:,2),uh,'linear','extrap');
rms = sqrt(mean((Rh-sig_ref).^2));
fprintf('  pico Lab    = %.4f MPa     pico Abaqus = %.4f MPa\n', max(Rh), max(ref(:,2)));
fprintf('  dt final Lab= %.4f          dt Abaqus   = %.4f\n', dth(end), ref(end,3));
fprintf('  RMS = %.4f MPa  (%.2f %% del pico)\n', rms, 100*rms/max(ref(:,2)));
fprintf('  tiempo = %.2f s\n', t);

figure('Color','w');
subplot(1,2,1); hold on; grid on;
plot(ref(:,1)*1000, ref(:,2), '-', 'Color',[.85 .3 .3], 'LineWidth',3, 'DisplayName','Abaqus CPS4');
plot(uh*1000, Rh, 'ko--', 'MarkerSize',3, 'DisplayName','Q4 Hekatan Lab');
xlabel('\epsilon_{xx} (\times10^{-3})'); ylabel('\sigma_{xx} (MPa)');
title('Traccion: Q4 vs Abaqus'); legend('Location','northeast');
subplot(1,2,2); hold on; grid on;
plot(ref(:,1)*1000, ref(:,3), '-', 'Color',[.85 .3 .3], 'LineWidth',3, 'DisplayName','Abaqus DAMAGET');
plot(uh*1000, dth, 'ko--', 'MarkerSize',3, 'DisplayName','dt Hekatan Lab');
xlabel('\epsilon_{xx} (\times10^{-3})'); ylabel('dano a traccion d_t');
title('Dano a traccion'); legend('Location','southeast');
end

%% ------------------------------------------ PARTE B: panel con entalla -----
function parte_B()
fprintf('\n==================================================================\n');
fprintf('PARTE B - Panel de Q4 con entalla: el dano LOCALIZA\n');
fprintf('==================================================================\n');
tic;
nx = 10; ny = 10; L = 600; H = 600;   % malla mas fina: la banda de dano se resuelve mejor
[nodos,elems] = malla_panel(nx,ny,L,H);
% ENTALLA REAL: se quita el elemento del borde izquierdo en la fila central.
% Sin entalla la grieta igual se localiza, pero por redondeo numerico: la
% fila que agrieta dependeria de la malla y no del modelo.
ent = (floor(ny/2))*nx + 1;
keep = true(size(elems,1),1); keep(ent) = false;
elems = elems(keep,:);
fprintf('  malla: %d nodos, %d elementos Q4 (1 quitado = entalla)\n', size(nodos,1), size(elems,1));

tol = 1e-9;
abajo  = find(nodos(:,2) < tol);
arriba = find(nodos(:,2) > H-tol);
fijos = [2*abajo.' ,  2*abajo(1)-1];       % uy=0 abajo, ux=0 en una esquina
gimp  = 2*arriba.';                        % uy impuesto arriba
[uh,Rh,dth,dte] = fem_q4(nodos,elems,fijos,gimp,0.35,40,100);
t = toc;

[pk,ip] = max(Rh);
fprintf('  carga pico = %.1f N  en u = %.4f mm\n', pk, uh(ip));
fprintf('  dano medio final = %.3f\n', dth(end));
fprintf('  tiempo = %.2f s\n', t);

figure('Color','w');
subplot(1,2,1); grid on;
plot(uh, Rh/1000, 'k-o', 'LineWidth',1.5, 'MarkerSize',3);
xlabel('desplazamiento impuesto u [mm]'); ylabel('carga P [kN]');
title('Curva carga-desplazamiento');

subplot(1,2,2);
V = nodos; Fc = elems;
patch('Vertices',V, 'Faces',Fc, 'FaceVertexCData',dte, ...
      'FaceColor','flat', 'EdgeColor',[.2 .2 .2]);
colormap(flipud(jet));          % jet invertido: rojo = mas dano
caxis([0 max(max(dte),1e-6)]); colorbar;
axis equal; axis([0 L 0 H]);
xlabel('X [mm]'); ylabel('Y [mm]');
title('Dano a traccion d_t (la grieta sale de la entalla)');
end

function [nodos,elems] = malla_panel(nx,ny,L,H)
xs = linspace(0,L,nx+1); ys = linspace(0,H,ny+1);
nodos = zeros((nx+1)*(ny+1),2); k = 0;
for j = 1:ny+1
    for i = 1:nx+1
        k = k+1; nodos(k,:) = [xs(i) ys(j)];
    end
end
elems = zeros(nx*ny,4); k = 0;
for j = 1:ny
    for i = 1:nx
        n0 = (j-1)*(nx+1)+i; k = k+1;
        elems(k,:) = [n0 n0+1 n0+nx+2 n0+nx+1];
    end
end
end
