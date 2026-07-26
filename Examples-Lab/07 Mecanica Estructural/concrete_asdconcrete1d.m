%% ASDConcrete1D — hormigon uniaxial plastico-dano (esfuerzo efectivo + IMPL-EX)
% -------------------------------------------------------------------------
% Porta la FORMULACION EXACTA del material ASDConcrete1D de ASDEA (OpenSees).
%   Massimo Petracca, Guido Camata - ASDEA Software, Italy
%   "A Simple and robust plastic-damage model for concrete and masonry"
%   Fuente: SRC/material/uniaxial/ASDConcrete1DMaterial.cpp  (.h)
%
% A diferencia de concrete_damage.m (elasticidad danada escalar, simplificada),
% este modelo es el NUCLEO real de ASDConcrete1D: dos leyes de endurecimiento
% independientes (traccion / compresion), esfuerzo EFECTIVO con dano de
% plasticidad, split traccion/compresion con Heaviside (cierre de fisura /
% recuperacion de rigidez al invertir) y extrapolacion IMPL-EX.
%
% ================= NUCLEO PORTADO — compute() (cpp l.1538-1624) ============
% Por paso, sobre el estado COMMITTED (xt, xc, seff, eps, PT):
%   1) Esfuerzo efectivo elastico  (l.1560-1561):
%        dEps  = eps - eps_commit
%        seff += E*dEps                      % predictor efectivo
%   2) Split traccion/compresion  (l.1564-1567):
%        PT = Heaviside(seff)  (1 tracc, 0 compr, 0.5 en 0)   % l.1564
%        ST = PT*seff ;  SC = (1-PT)*seff
%      -> En IMPL-EX PT se CONGELA en PT_commit (l.1564) = cierre de fisura
%         diferido; el signo del esf. efectivo decide que dano actua.
%   3) Ley de endurecimiento committed  (l.1570-1575):
%        pt = evalAt(ht, xt) ;  pc = evalAt(hc, xc)
%        xt_pl = pt.x - pt.q/E   (def. plastica, l.56)   idem xc_pl
%   4) Nuevas medidas de deformacion equivalente (l.1578-1592):
%      IMPLICITO:  xt_tr =  ST/E + xt_pl ;  xc_tr = -SC/E + xc_pl
%                  si xt_tr > pt.x : xt = rc1*pt.x + rc2*xt_tr   (historico)
%                  si xc_tr > pc.x : xc = rc1*pc.x + rc2*xc_tr
%      IMPL-EX  :  xt = xt_c + tf*(xt_c - xt_c_old)   (extrapolacion explicita)
%                  xc = xc_c + tf*(xc_c - xc_c_old)
%      (rc1=eta/(eta+dt), rc2=dt/(eta+dt); eta=0 -> rc1=0, rc2=1)
%   5) Re-evaluar pt, pc en xt, xc  (l.1593-1594).
%   6) Dano de PLASTICIDAD (esfuerzo efectivo, l.1597-1600):
%        seff_eq_t = (pt.x - xt_pl)*E ;  dt_pl = 1 - pt.q/seff_eq_t
%        seff_eq_c = (pc.x - xc_pl)*E ;  dc_pl = 1 - pc.q/seff_eq_c
%   7) Esfuerzo efectivo actualizado  (l.1603):
%        seff = (1-dt_pl)*ST + (1-dc_pl)*SC
%   8) Dano NOMINAL combinado (curva + plasticidad, l.1606-1608):
%        dt_bar = pt.d + dt_pl - pt.d*dt_pl        % pt.d = dano de la curva -Td
%        dc_bar = pc.d + dc_pl - pc.d*dc_pl
%        stress = (1-dt_bar)*ST + (1-dc_bar)*SC    % esfuerzo nominal (salida)
%   9) Tangente secante  (l.1611-1613):
%        W = 1 - dt_bar*PT - dc_bar*PC ;  C = W*E
%
% commitState (cpp l.1094-1124): xt_c_old<-xt_c ; xt_c<-xt (idem xc); guarda
% eps, stress, seff, PT_commit. xt/xc son MONOTONAS no decrecientes (memoria).
%
% ---- CIERRE DE FISURA / RECUPERACION DE RIGIDEZ (efecto unilateral) -------
% El split usa PT=Heaviside(seff). En traccion (seff>0) solo dt_bar reduce el
% esfuerzo; al invertir a compresion (seff<0) PT=0 -> ST=0, y el dano de
% traccion dt_bar deja de actuar: la rigidez de compresion se RECUPERA a
% (1-dc_bar)*E (la fisura se cierra). Analogamente al revertir a traccion.
%
% ---- IMPL-EX (cpp setTrialStrain l.1038-1062 + commit l.1097-1108) --------
% La fase EXPLICITA extrapola xt, xc del historico committed y CONGELA el
% split (PT_commit) -> tangente = secante, robusta. La fase IMPLICITA (en el
% commit) recalcula el estado real y mide el error IMPL-EX = max(|dt-dt_imp|,
% |dc-dc_imp|). Aqui se corre una pasada implicita (backbone exacto) y una
% pasada IMPL-EX superpuesta para comparar.
%
% ================= CONSTRUCCION DE LAS LEYES (ASDConcrete1D_MakeLaws) =======
% Las curvas -Te/-Ts/-Td (traccion) y -Ce/-Cs/-Cd (compresion) se generan de
% (E, ft, fc) igual que STKO  (OPS_ASDConcrete1DMaterial, cpp l.353-455):
%   Gt = 0.073*fc^0.18 ;  Gc = 2*Gt*fc^2/ft^2   (energias de fractura)
%   lch_ref = min(hmin_c, hmin_t)               (l.364-375, auto)
%   Traccion  _make_tension     (l.379-413)  -> pico ft
%   Compresion _make_compression (l.416-455)  -> pico fc via bezier3
% Cada punto guarda q=(x-xpl)*E (esf. efectivo) y d=1-y/q (dano de la curva).
% Con E=27606, ft=3.1, fc=34.5 reproduce las curvas strain-loc de STKO:
%   Te=[0 1.011e-4 1.684e-4 5.693e-3 2.813e-2 ...]  Ts=[0 2.79 3.1 0.62 0.0031]
%
% Convencion: eps>0 traccion, eps<0 compresion. Las leyes se guardan en
% valores ABSOLUTOS (positivos), como en el constructor (cpp l.544-545).
% -------------------------------------------------------------------------
clear; clc; close all;

%% Propiedades del hormigon (entradas de ASDConcrete1D)
E  = 27606;    % MPa   modulo elastico
ft = 3.1;      % MPa   resistencia a traccion
fc = 34.5;     % MPa   resistencia a compresion
eta = 0;       % s     viscosidad (0 = rate-independent)

%% ---- Energias de fractura y longitud de referencia (cpp l.354-375) ----
Gt = 0.073*fc^0.18;             % energia de fractura en traccion  (l.355)
Gc = 2*Gt*(fc^2)/(ft^2);        % energia de fractura en compresion (l.356)
ec = 2*fc/E;                    % def. de pico en compresion       (l.354)
% lch_ref automatico = min longitud de localizacion (l.364-375)
et_el  = ft/E;                  ec1 = fc/E;
Gt_min = 0.5*ft*et_el;          hmin_t = 0.01*Gt/Gt_min;
ec_pl0 = (ec-ec1)*0.4 + ec1;    Gc_min = 0.5*fc*(ec-ec_pl0);
hmin_c = 0.01*Gc/Gc_min;
lch_ref = min(hmin_c, hmin_t);  % l.375

%% ---- _make_tension : curva -Te/-Ts/-Td (cpp l.379-413) ----
GtR = Gt/lch_ref;               % energia especifica regularizada (l.382)
f0 = 0.9*ft;  f1 = ft;
e0 = f0/E;    e1 = 1.5*f1/E;    ep = e1 - f1/E;
f2 = 0.2*ft;  f3 = 1.0e-3*ft;
w2 = GtR/ft;  w3 = 5.0*w2;
e2 = w2 + f2/E + ep;   if e2 <= e1, e2 = 1.001*e1; end
e3 = w3 + f3/E + ep;   if e3 <= e2, e3 = 1.001*e2; end
e4 = 10.0*e3;
Te  = [0, e0, e1, e2, e3, e4];
Ts  = [0, f0, f1, f2, f3, f3];
Tpl = [0, 0,  ep, 0.9*e2, 0.8*e3, 0.8*e3];
Tq  = zeros(1,6);  Td = zeros(1,6);
Tq(2) = E*Te(2);                     % elastico: q1 = E*e0 = f0
for i = 3:6                          % i=2..5 (0-based) -> 3..6 (1-based), l.405-413
    xi = Te(i); si = Ts(i);
    xipl = min(Tpl(i), xi - si/E);   % l.409-410
    Tq(i) = (xi - xipl)*E;           % q = esfuerzo efectivo (l.411)
    Td(i) = 1 - si/Tq(i);            % dano de la curva (l.412)
end
laT.X = Te; laT.Y = Ts; laT.Q = Tq; laT.D = Td;
laT.xtol = 1e-12; laT.ytol = 1e-12;

%% ---- _make_compression : curva -Ce/-Cs/-Cd via bezier3 (cpp l.416-455) ----
GcR = Gc/lch_ref;               % l.419
fc0 = 0.5*fc;  ec0 = fc0/E;  ec1 = fc/E;  fcr = 0.1*fc;
ec_pl = (ec-ec1)*0.4 + ec1;
Gc1 = 0.5*fc*(ec-ec_pl);
Gc2 = max(0.01*Gc1, GcR - Gc1);
ecr = ec + 2.0*Gc2/(fc+fcr);
nc = 10;
np = nc + 3;                    % 13 puntos
Ce = zeros(1,np); Cs = zeros(1,np); Cpl = zeros(1,np);
Ce(2) = ec0;  Cs(2) = fc0;      % Ce[0]=0, Ce[1]=ec0 (l.430-432)
dec = (ec - ec0)/(nc - 1);
for i = 0:nc-2                  % l.434-439
    iec = ec0 + (i+1)*dec;
    k = i + 3;                  % Ce[i+2] 0-based -> k 1-based
    Ce(k) = iec;
    Cs(k) = bezier3(iec, ec0, ec1, ec, fc0, fc, fc);
    Cpl(k) = Cpl(k-1) + 0.7*(iec - Cpl(k-1));
end
Ce(nc+2) = ecr;        Cs(nc+2) = fcr;                       % l.440-441
Cpl(nc+2) = Cpl(nc+1) + 0.7*(ecr - Cpl(nc+1));
Ce(nc+3) = ecr + ec0;  Cs(nc+3) = fcr;  Cpl(nc+3) = Cpl(nc+2); % l.443-445
Cq = zeros(1,np); Cd = zeros(1,np);
Cq(2) = E*Ce(2);
for i = 3:np                   % l.447-455
    xi = Ce(i); si = Cs(i);
    xipl = min(Cpl(i), xi - si/E);
    Cq(i) = (xi - xipl)*E;
    Cd(i) = 1 - si/Cq(i);
end
laC.X = Ce; laC.Y = Cs; laC.Q = Cq; laC.D = Cd;
laC.xtol = 1e-12; laC.ytol = 1e-12;

%% Empaquetar material
mp.E = E; mp.eta = eta; mp.alpha = 1.0; mp.dtime = 1.0;
mp.T = laT; mp.C = laC;

fprintf('=== ASDConcrete1D — hormigon plastico-dano (ASDEA) ===\n\n');
fprintf('E=%.0f  ft=%.2f  fc=%.2f MPa\n', E, ft, fc);
fprintf('Gt=%.4f  Gc=%.3f MPa*mm   lch_ref=%.3f mm (auto)\n', Gt, Gc, lch_ref);
fprintf('Traccion  Te=[%.3e %.3e %.3e %.3e %.3e]\n', Te(1:5));
fprintf('          Ts=[%.4f %.4f %.4f %.4f %.4f]\n', Ts(1:5));
fprintf('          Td=[%.3f %.3f %.3f %.3f %.3f]\n', Td(1:5));
fprintf('Compresion pico fc=%.2f en ec=%.3e ; residual fcr=%.2f\n\n', fc, ec, fcr);

%% Protocolo ciclico de deformacion con reversas
% Compresion creciente con descargas + excursion de traccion (fisura) +
% recarga en compresion (cierre de fisura) + gran traccion final.
pts  = [0, -0.0018, -0.0005, -0.0040, -0.0010, +0.0012, -0.0010, -0.0300, +0.0045, 0];
nseg = 100;                    % subpasos por segmento (integracion fina)
eps  = pts(1);
for s = 1:numel(pts)-1
    seg = linspace(pts(s), pts(s+1), nseg+1);
    eps = [eps, seg(2:end)];   % sin repetir la esquina
end
Ns = numel(eps);
fprintf('Protocolo: %d esquinas, %d subpasos/seg -> %d pasos\n\n', ...
        numel(pts), nseg, Ns);

%% ---------- PASADA IMPLICITA (backbone exacto) ----------
% Estado committed: eps_c, seff_c, xt, xc, xt_old, xc_old, PT_c
st.eps_c = 0; st.seff_c = 0; st.xt = 0; st.xc = 0;
st.xt_old = 0; st.xc_old = 0; st.PT_c = 0.5;

sig = zeros(1,Ns); dt = zeros(1,Ns); dc = zeros(1,Ns);
xtv = zeros(1,Ns); xcv = zeros(1,Ns); Cst = zeros(1,Ns);
for k = 1:Ns
    [so, seff, xt, xc, dtb, dcb, PT, Ck] = mat_compute(st, eps(k), mp, false);
    sig(k)=so; dt(k)=dtb; dc(k)=dcb; xtv(k)=xt; xcv(k)=xc; Cst(k)=Ck;
    % commit (cpp l.1113-1119)
    st.xt_old = st.xt; st.xc_old = st.xc;
    st.xt = xt; st.xc = xc;
    st.eps_c = eps(k); st.seff_c = seff; st.PT_c = PT;
end

%% ---------- PASADA IMPL-EX (explicita + control implicito) ----------
s2.eps_c = 0; s2.seff_c = 0; s2.xt = 0; s2.xc = 0;
s2.xt_old = 0; s2.xc_old = 0; s2.PT_c = 0.5;
sigx = zeros(1,Ns); errx = zeros(1,Ns);
for k = 1:Ns
    % fase EXPLICITA: extrapola x, congela PT -> esfuerzo reportado
    [sox, ~, ~, ~, dtx, dcx, ~, ~] = mat_compute(s2, eps(k), mp, true);
    sigx(k) = sox;
    % fase IMPLICITA (commit): estado real + error IMPL-EX
    [~, seffi, xti, xci, dti, dci, PTi, ~] = mat_compute(s2, eps(k), mp, false);
    errx(k) = max(abs(dtx-dti), abs(dcx-dci));
    s2.xt_old = s2.xt; s2.xc_old = s2.xc;
    s2.xt = xti; s2.xc = xci;
    s2.eps_c = eps(k); s2.seff_c = seffi; s2.PT_c = PTi;
end

%% Verificacion del comportamiento fisico
fprintf('=== Verificacion del comportamiento fisico ===\n');
% 1) pico de traccion ~ ft
[smax, imax] = max(sig);                 % por-columna (vector fila -> escalar OK)
fprintf('Pico traccion:    sigma=%.2f MPa a eps=%+.5f  (ft=%.2f)\n', ...
        smax, eps(imax), ft);
% 2) pico de compresion ~ fc
[smin, imin] = min(sig);
fprintf('Pico compresion:  sigma=%.2f MPa a eps=%+.5f  (fc=%.2f)\n', ...
        smin, eps(imin), fc);
% 3) softening de traccion: cae tras fisurar
idcrk = find(dt > 0.05); kcrk = idcrk(1); % find(...,1,'first') no soportado
fprintf('Softening tracc.: dt supera 0.05 en eps=%+.5f (inicio de fisura)\n', ...
        eps(kcrk));
% 4) recuperacion de rigidez (cierre de fisura): tras dano de traccion,
%    al recargar en compresion la tangente vuelve a (1-dc)*E, no (1-dt)*E.
%    Se busca un paso en compresion (seff<0) con dt alto ya acumulado.
kclose = 0;
for k = 2:Ns
    if eps(k) < eps(k-1) && eps(k) < 0 && dt(k) > 0.3 && dc(k) < dt(k)
        kclose = k; break;
    end
end
if kclose > 0
    Crec = Cst(kclose)/E;
    fprintf('Cierre de fisura: en eps=%+.5f dt=%.2f pero C/E=%.2f (~1-dc=%.2f)\n', ...
            eps(kclose), dt(kclose), Crec, 1-dc(kclose));
    fprintf('                  la rigidez de compresion se RECUPERA (no queda 1-dt=%.2f)\n', ...
            1-dt(kclose));
end
% 5) degradacion de rigidez por dano
fprintf('Rigidez secante:  C/E inicial=%.3f  final=%.3f (degradada por dano)\n', ...
        Cst(1)/E, Cst(end)/E);
% 6) dano final
fprintf('Dano final:       dt=%.3f  dc=%.3f\n', dt(end), dc(end));
% 7) error IMPL-EX
fprintf('IMPL-EX:          error max=%.4f  (extrapolacion vs implicito)\n\n', max(errx));

%% Grafica 1 — HISTERESIS sigma-eps (softening + degradacion por dano)
figure;
plot(eps*1000, sig, 'b-', 'LineWidth', 1.4); hold on;
plot(eps*1000, sigx, 'r--', 'LineWidth', 0.9);       % IMPL-EX superpuesto
scatter(eps*1000, sig, 12, max(dt,dc), 'filled');     % coloreado por dano
colormap(flipud(jet)); cb = colorbar; cb.Label.String = 'dano  max(d_t,d_c) [-]';  % jet_r portable (MATLAB 2017a no tiene jet_r)
caxis([0 1]); grid on;
xlabel('deformacion  \epsilon  [milesimas]');
ylabel('esfuerzo  \sigma  [MPa]');
title('ASDConcrete1D: histeresis \sigma-\epsilon (softening, dano, cierre de fisura)');
% referencias de resistencia
xr = [min(eps) max(eps)]*1000;
plot(xr, [ ft  ft], 'k:'); plot(xr, [-fc -fc], 'k:');
text(xr(2), ft, '  f_t', 'VerticalAlignment', 'bottom');
text(xr(1), -fc, ' -f_c', 'VerticalAlignment', 'top');
legend('implicito (backbone)', 'IMPL-EX (explicito)', 'Location', 'southeast');

%% Grafica 2 — Evolucion del dano de traccion dt y compresion dc
figure;
plot(1:Ns, dt, 'r-', 'LineWidth', 1.8); hold on;
plot(1:Ns, dc, 'b-', 'LineWidth', 1.8);
grid on; ylim([0 1.05]);
xlabel('paso del protocolo  k');
ylabel('dano  [-]');
title('ASDConcrete1D: evolucion de los danos d_t (traccion) y d_c (compresion)');
legend('d_t  (fisuracion / traccion)', 'd_c  (aplastamiento / compresion)', ...
       'Location', 'northwest');

%% Grafica 3 — Rigidez secante C/E y protocolo de deformacion vs paso
figure;
subplot(2,1,1);
plot(1:Ns, Cst/E, 'k-', 'LineWidth', 1.6); grid on; ylim([0 1.05]);
ylabel('C / E  [-]');
title('ASDConcrete1D: degradacion de la rigidez secante C/E');
subplot(2,1,2);
plot(1:Ns, eps*1000, 'm-', 'LineWidth', 1.4); grid on;
xlabel('paso del protocolo  k');
ylabel('\epsilon  [milesimas]');
title('protocolo de deformacion (compresion con descargas + reversas de traccion)');

%% Grafica 4 — Leyes de endurecimiento -Te/-Ts (traccion) y -Ce/-Cs (compresion)
figure;
xt_plot = linspace(0, 1.2*Te(5), 300);
st_plot = zeros(1,300);
for j = 1:300, st_plot(j) = law_stress(laT, xt_plot(j)); end
xc_plot = linspace(0, 0.06, 300);
sc_plot = zeros(1,300);
for j = 1:300, sc_plot(j) = law_stress(laC, xc_plot(j)); end
subplot(1,2,1);
plot(xt_plot*1000, st_plot, 'r-', 'LineWidth', 1.8); hold on;
plot(Te*1000, Ts, 'ko', 'MarkerFaceColor', 'w'); grid on;
xlabel('\epsilon_t  [milesimas]'); ylabel('\sigma_t  [MPa]');
title('Ley traccion -Te/-Ts');
subplot(1,2,2);
plot(xc_plot*1000, sc_plot, 'b-', 'LineWidth', 1.8); hold on;
plot(Ce*1000, Cs, 'ko', 'MarkerFaceColor', 'w'); grid on;
xlabel('\epsilon_c  [milesimas]'); ylabel('\sigma_c  [MPa]');
title('Ley compresion -Ce/-Cs (bezier3)');

fprintf('Listo — 4 graficas: histeresis, danos dt/dc, rigidez+protocolo, leyes.\n');

%% ==================== NUCLEO CONSTITUTIVO ASDConcrete1D ==================
function [stress, seff, xt, xc, dt_bar, dc_bar, PT, Cst] = mat_compute(st, strain, mp, do_implex)
    % Un paso de compute() de ASDConcrete1D (cpp l.1538-1624), sin commitear.
    % do_implex=false -> implicito (backbone); true -> extrapolacion IMPL-EX.
    E = mp.E; eta = mp.eta; dtime = mp.dtime;

    % variables committed
    xt = st.xt; xc = st.xc; seff = st.seff_c;

    % coeficientes de tasa (viscosidad), cpp l.1552-1557
    rc1 = 0.0; rc2 = 1.0;
    if dtime > 0 && eta > 0
        rc1 = eta/(eta+dtime); rc2 = dtime/(eta+dtime);
    end
    tf = 1.0;                                  % time_factor (dt const), l.1547-1549

    % esfuerzo efectivo elastico (predictor), cpp l.1560-1561
    seff = seff + E*(strain - st.eps_c);

    % split traccion/compresion, cpp l.1564-1567
    if do_implex
        PT = st.PT_c;                          % congelado en IMPL-EX
    else
        PT = (seff > 0) + 0.5*(seff == 0);     % Heaviside (l.92)
    end
    PC = 1 - PT;
    ST = PT*seff; SC = PC*seff;

    % ley committed y def. plastica vieja, cpp l.1570-1575
    [ptx, ptq, ~] = eval_law(mp.T, xt);
    [pcx, pcq, ~] = eval_law(mp.C, xc);
    xt_pl = ptx - ptq/E;
    xc_pl = pcx - pcq/E;

    % nuevas medidas de deformacion equivalente, cpp l.1578-1592
    if do_implex
        xt = st.xt + tf*(st.xt - st.xt_old);   % extrapolacion explicita
        xc = st.xc + tf*(st.xc - st.xc_old);
    else
        xt_trial =  ST/E + xt_pl;
        xc_trial = -SC/E + xc_pl;
        if xt_trial > ptx, xt = rc1*ptx + rc2*xt_trial; end   % historico (crece)
        if xc_trial > pcx, xc = rc1*pcx + rc2*xc_trial; end
    end
    [ptx, ptq, ptd] = eval_law(mp.T, xt);
    [pcx, pcq, pcd] = eval_law(mp.C, xc);

    % dano de plasticidad (esfuerzo efectivo), cpp l.1597-1600
    seff_eq_t = (ptx - xt_pl)*E;  dt_pl = 0; if seff_eq_t > 0, dt_pl = 1 - ptq/seff_eq_t; end
    seff_eq_c = (pcx - xc_pl)*E;  dc_pl = 0; if seff_eq_c > 0, dc_pl = 1 - pcq/seff_eq_c; end

    % esfuerzo efectivo actualizado, cpp l.1603
    seff = (1-dt_pl)*ST + (1-dc_pl)*SC;

    % dano nominal combinado + esfuerzo nominal, cpp l.1606-1608
    dt_bar = ptd + dt_pl - ptd*dt_pl;
    dc_bar = pcd + dc_pl - pcd*dc_pl;
    stress = (1-dt_bar)*ST + (1-dc_bar)*SC;

    % tangente secante, cpp l.1611-1613
    W = 1 - dt_bar*PT - dc_bar*PC;
    Cst = W*E;
end

function [px, pq, pd] = eval_law(law, x)
    % HardeningLaw::evaluateAt (cpp l.665-709): interpola x,y,q y d=1-y/q.
    X = law.X; Y = law.Y; Q = law.Q; n = numel(X);
    found = false; x1=0; x2=0; y1=0; y2=0; q1=0; q2=0;
    for i = 2:n
        if x <= X(i) + law.xtol
            x1=X(i-1); x2=X(i); y1=Y(i-1); y2=Y(i); q1=Q(i-1); q2=Q(i);
            found = true; break;
        end
    end
    if ~found                                  % mas alla del ultimo punto (l.687-700)
        x1 = X(n); x2 = x; span = x - x1;
        y1 = Y(n); tang = (y1 - Y(n-1))/(x1 - X(n-1));
        if tang > 0, y2 = y1 + span*tang; else, y2 = y1; end
        q1 = Q(n); tang = (q1 - Q(n-1))/(x1 - X(n-1));
        if tang > 0, q2 = q1 + span*tang; else, q2 = q1; end
    end
    xspan = x2 - x1;
    if xspan > 0, xr = (x - x1)/xspan; else, xr = 0; end
    py = max(law.ytol, y1 + (y2-y1)*xr);
    pq = max(law.ytol, q1 + (q2-q1)*xr);
    pd = 1 - py/pq;
    px = x;
end

function s = law_stress(law, x)
    % esfuerzo nominal de la curva en x (para graficar la backbone)
    X = law.X; Y = law.Y; n = numel(X);
    found = false; x1=0; x2=0; y1=0; y2=0;
    for i = 2:n
        if x <= X(i) + law.xtol
            x1=X(i-1); x2=X(i); y1=Y(i-1); y2=Y(i); found = true; break;
        end
    end
    if ~found
        x1 = X(n); x2 = x; span = x - x1;
        y1 = Y(n); tang = (y1 - Y(n-1))/(x1 - X(n-1));
        if tang > 0, y2 = y1 + span*tang; else, y2 = y1; end
    end
    xspan = x2 - x1;
    if xspan > 0, xr = (x - x1)/xspan; else, xr = 0; end
    s = max(law.ytol, y1 + (y2-y1)*xr);
end

function y = bezier3(xi, x0, x1, x2, y0, y1, y2)
    % Curva de Bezier cuadratica racional para la rama de compresion (cpp l.137-157)
    A = x0 - 2.0*x1 + x2;
    B = 2.0*(x1 - x0);
    C = x0 - xi;
    if abs(A) < 1.0e-12
        x1 = x1 + 1.0e-6*(x2 - x0);
        A = x0 - 2.0*x1 + x2;
        B = 2.0*(x1 - x0);
        C = x0 - xi;
    end
    if A == 0.0, y = 0.0; return; end
    D = B*B - 4.0*A*C;
    t = (sqrt(D) - B)/(2.0*A);
    y = (y0 - 2.0*y1 + y2)*t*t + 2.0*(y1 - y0)*t + y0;
end
