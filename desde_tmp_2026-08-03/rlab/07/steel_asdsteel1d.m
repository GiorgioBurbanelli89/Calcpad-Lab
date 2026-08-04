%% ASDSteel1D — acero uniaxial plastico-dano (Chaboche + fractura) ciclico
% -------------------------------------------------------------------------
% Porta la FORMULACION del material ASDSteel1D de ASDEA (OpenSees) a Lab.
%   Alessia Casalucci, Massimo Petracca, Guido Camata - ASDEA Software 2025
%   "A unified and efficient plastic-damage material model for steel bars
%    including fracture, bond-slip, and buckling via multiscale homogenization"
%   Fuente: SRC/material/uniaxial/ASDSteel1DMaterial.cpp  (SteelComponent::compute)
%
% NUCLEO PORTADO (respuesta uniaxial base del acero):
%   1) PLASTICIDAD J2-1D con endurecimiento CINEMATICO de CHABOCHE de 2
%      back-stresses (alpha1, alpha2) -> efecto Bauschinger.
%   2) Return-mapping IMPLICITO (Newton sobre el multiplicador plastico
%      delta_lambda), con integracion EXPONENTIAL de los back-stresses.
%   3) DANO por FRACTURA que inicia al superar la deformacion plastica
%      ultima (derivada de eu) con ablandamiento exponencial regularizado.
%   Add-ons NO portados aqui (multiescala): bond-slip y pandeo (buckling por
%   homogenizacion del RVE de 4 nodos). Ver "Notas" al final.
%
% -------- Mapeo termino a termino con el .cpp de ASDEA --------------------
% Predictor elastico  (cpp l.341-344):
%     sigma_tr = stress_commit + E*(eps - eps_commit)
% Esfuerzo relativo   (lam_rel_stress, l.347):  xi = sigma - alpha1 - alpha2
% Funcion de fluencia (lam_yield_function, l.350): F = |xi| - sy
% Direccion de flujo :  sg = sign(xi)
% Derivada de F resp. lambda (lam_yield_derivative, l.353-361):
%     dF/dl = sg*( -E*sg - (H1*sg - g1*alpha1) - (H2*sg - g2*alpha2) )
% Actualizacion (lam_yield_update, l.363-370) — Chaboche EXPONENTIAL:
%     sigma  -= sg*dlambda*E
%     alpha_i = sg*Hi/gi - (sg*Hi/gi - alpha_i^commit)*exp(-gi*delta_lambda)
% Deformacion plastica POSITIVA (para fractura, l.414-415):
%     if sg>0:  epl += sg*delta_lambda
% Tangente mezclada K_alpha (l.420) — solo informativa aqui.
%
% -------- Parametros de Chaboche derivados de (E, sy, su, eu) -------------
% (cpp l.2048-2070). Se eligen para que la pendiente inicial ~ E y el
% esfuerzo se acerque a 'su' cuando la deformacion llega a 'eu':
%     n=400, m=50, alpha=0.9
%     H1  = E/(n*eu)                 g1 = H1/(E) * E/(su-sy) = (E/(n*eu))/((su-sy)/E)
%     H2  = m*H1                     g2 = m*g1
%     H1_par = alpha*H1              H2_par = (1-alpha)*H2
%   Saturacion:  H1_par/g1 + H2_par/g2 = (su-sy)  ->  sigma_pico -> su.
%
% -------- Dano por fractura (cpp l.2256-2273) ----------------------------
%     eupl    = eu - sy/E                         (def. plastica ultima)
%     epl_max = 2*eupl        (sin regularizacion; con buckling usa lch/r)
%     G       = (epl_max - eupl)*sy/4             (energia de fractura reg.)
%     if epl > eupl:
%         sig_d = max(1e-4*sy, sy*exp(-sy*(epl-eupl)/G))
%         d     = 1 - sig_d/sy
%     sigma_out = (1 - d)*sigma      % plasticidad en espacio de esf. efectivo
%
% El return-mapping opera sobre el esfuerzo EFECTIVO (sin danar); el dano
% escala la SALIDA (split plastico-dano estandar), por eso stress_commit
% que alimenta el predictor es el efectivo sin danar.
%
% Convencion: eps>0 traccion, eps<0 compresion.
% -------------------------------------------------------------------------
clear; clc; close all;

%% Propiedades del acero de refuerzo (entradas base de ASDSteel1D)
E   = 200000;   % MPa   modulo de Young
sy  = 420;      % MPa   esfuerzo de fluencia
su  = 600;      % MPa   esfuerzo ultimo (saturacion de los back-stresses)
eu  = 0.12;     %       deformacion ultima (inicio del dano por fractura)

%% Parametros de Chaboche derivados (cpp CreateMaterial l.2048-2070)
n = 400; m = 50; alpha = 0.9;
H1 = E/(n*eu);              % H1_norm*E,  H1_norm=(1/n)/eu
g1 = (H1/E)/((su-sy)/E);   % gamma1 = H1_norm/dy_norm
H2 = m*H1;                 % H2 = H1*m
g2 = m*g1;                 % gamma2 = gamma1*m
% parametros repartidos entre las dos superficies
H1p = alpha*H1;      g1p = g1;
H2p = (1-alpha)*H2;  g2p = g2;

% empaquetar en struct de material (mp) — se copia por valor
mp.E = E;  mp.sy = sy;  mp.su = su;  mp.eu = eu;
mp.H1 = H1p; mp.g1 = g1p; mp.H2 = H2p; mp.g2 = g2p;
% fractura
mp.eupl    = eu - sy/E;                 % def. plastica ultima
mp.epl_max = 2*mp.eupl;                 % sin regularizacion (2*eupl)
mp.G       = (mp.epl_max - mp.eupl)*sy/4;

fprintf('=== ASDSteel1D — acero plastico-dano (Chaboche + fractura) ===\n\n');
fprintf('E=%.0f  sy=%.0f  su=%.0f MPa   eu=%.3f\n', E, sy, su, eu);
fprintf('Chaboche:  H1=%.1f  g1=%.2f  |  H2=%.1f  g2=%.2f\n', H1p, g1p, H2p, g2p);
fprintf('Saturacion H1/g1 + H2/g2 = %.1f MPa  (su-sy = %.1f)\n', ...
        H1p/g1p + H2p/g2p, su-sy);
fprintf('Fractura:  eupl=%.4f  epl_max=%.4f  G=%.3f\n\n', ...
        mp.eupl, mp.epl_max, mp.G);

%% Protocolo ciclico: reversas de AMPLITUD CRECIENTE + jalon final a fractura
% (ensayo tipico de acero: lazos de Bauschinger que crecen, luego rotura)
pts = [0, 0.006, -0.006, 0.012, -0.012, 0.020, -0.020, ...
       0.030, -0.030, 0.050, -0.030, 0.140];
nseg = 60;                      % subpasos por segmento (integracion fina)

% Construir la senal de deformacion recorriendo cada segmento
eps = pts(1);
for s = 1:numel(pts)-1
    seg = linspace(pts(s), pts(s+1), nseg+1);
    eps = [eps, seg(2:end)];    % sin repetir la esquina
end
Ns = numel(eps);
fprintf('Protocolo: %d esquinas, %d subpasos/seg -> %d pasos\n\n', ...
        numel(pts), nseg, Ns);

%% Loop de la maquina de estado (estado persistente entre pasos)
% Estado del material (committed): eps_c, sig_c (efectivo sin danar),
% back-stresses a1/a2, deformacion plastica acumulada epl.
st.eps_c = 0; st.sig_c = 0; st.a1 = 0; st.a2 = 0; st.epl = 0;

sig  = zeros(1, Ns);   % esfuerzo de salida (danado) [MPa]
dvar = zeros(1, Ns);   % variable de dano d [-]
eplv = zeros(1, Ns);   % def. plastica acumulada [-]
a1v  = zeros(1, Ns);   % back-stress 1 [MPa]
a2v  = zeros(1, Ns);   % back-stress 2 [MPa]

for k = 1:Ns
    [so, d, st] = steel_step(eps(k), st, mp);
    sig(k)  = so;
    dvar(k) = d;
    eplv(k) = st.epl;
    a1v(k)  = st.a1;
    a2v(k)  = st.a2;
end

%% Verificacion del comportamiento fisico
fprintf('=== Verificacion del comportamiento fisico ===\n');
% 1) fluencia inicial ~ sy (primer punto donde |sig| supera 0.99*sy)
idxy   = find(abs(sig) > 0.99*sy);     % find(...,1,'first') no soportado
kyield = idxy(1);
fprintf('Fluencia inicial: sigma=%.1f MPa a eps=%.5f  (sy=%.0f)\n', ...
        sig(kyield), eps(kyield), sy);
% 2) pico de esfuerzo (endurecimiento hacia su, capado por el dano) — por-columna
[smax, imax] = max(sig);
fprintf('Pico traccion:    sigma=%.1f MPa a eps=%.4f  (-> su=%.0f)\n', ...
        smax, eps(imax), su);
% 3) Bauschinger: la reversa fluye ANTES por el back-stress.
%    En el pico del 1er ciclo (esquina +0.006) el back-stress a=a1+a2>0;
%    al invertir, la fluencia en compresion ocurre en |sigma| = sy - a < sy.
irev   = 1 + nseg;                     % indice de la 1a esquina de traccion
a_rev  = a1v(irev) + a2v(irev);        % back-stress acumulado en la reversa
sy_rev = sy - a_rev;                   % esfuerzo de re-fluencia en compresion
fprintf('Bauschinger:      back-stress en reversa a=%.1f MPa -> re-fluencia\n', a_rev);
fprintf('                  en compresion a |sigma|=%.1f MPa < sy=%.0f\n', sy_rev, sy);
% 4) fractura: dano final y caida de esfuerzo
fprintf('Fractura:         epl_final=%.4f (eupl=%.4f)  d_final=%.3f\n', ...
        eplv(end), mp.eupl, dvar(end));
fprintf('Esfuerzo final (fracturado): sigma=%.2f MPa\n\n', sig(end));

%% Grafica 1 — HISTERESIS sigma-eps (lazos de Bauschinger + fractura)
figure;
plot(eps*100, sig, 'b-', 'LineWidth', 1.2); hold on;
scatter(eps*100, sig, 16, dvar, 'filled');     % coloreado por dano
colormap(jet_r); cb = colorbar; cb.Label.String = 'dano d [-]';
caxis([0 1]);
grid on;
xlabel('deformacion  \epsilon  [%]');
ylabel('esfuerzo  \sigma  [MPa]');
title('ASDSteel1D — histeresis \sigma(\epsilon): Bauschinger, endurecimiento, fractura');
% lineas de referencia sy y su
xr = [min(eps) max(eps)]*100;
plot(xr, [ sy  sy], 'k--'); plot(xr, [-sy -sy], 'k--');
plot(xr, [ su  su], 'r:');  plot(xr, [-su -su], 'r:');
text(xr(2), sy, '  +s_y', 'VerticalAlignment', 'bottom');
text(xr(2), su, '  +s_u', 'VerticalAlignment', 'bottom', 'Color', 'r');

%% Grafica 2 — Back-stresses de Chaboche vs paso (origen del Bauschinger)
figure;
plot(1:Ns, a1v, 'r-', 'LineWidth', 1.6); hold on;
plot(1:Ns, a2v, 'g-', 'LineWidth', 1.6);
plot(1:Ns, a1v+a2v, 'b-', 'LineWidth', 1.8);
grid on;
xlabel('paso del protocolo  k');
ylabel('back-stress  [MPa]');
title('ASDSteel1D — endurecimiento cinematico de Chaboche (\alpha_1, \alpha_2)');
legend('\alpha_1 (lento, g_1)', '\alpha_2 (rapido, g_2)', ...
       '\alpha_1+\alpha_2', 'Location', 'northwest');

%% Grafica 3 — Evolucion del dano y def. plastica acumulada (mismo eje)
figure;
plot(1:Ns, dvar, 'r-', 'LineWidth', 1.8); hold on;
plot(1:Ns, eplv, 'b-', 'LineWidth', 1.8);
% linea de inicio del dano  epl = eupl
plot([1 Ns], [mp.eupl mp.eupl], 'k--');
grid on;
xlabel('paso del protocolo  k');
ylabel('dano d [-]   /   \epsilon_{pl} acumulada [-]');
title('ASDSteel1D — inicio del dano al superar \epsilon_{pl}^u y degradacion');
legend('d (dano)', '\epsilon_{pl} (acumulada)', '\epsilon_{pl}^u (umbral)', ...
       'Location', 'west');

fprintf('Listo — 3 graficas: histeresis, back-stresses, dano/epl.\n');

%% ==================== NUCLEO CONSTITUTIVO ASDSteel1D =====================
function [sigma_out, d, st] = steel_step(eps_new, st, mp)
    % Un paso del return-mapping de ASDSteel1D (SteelComponent::compute).
    % Entrada: eps_new (deformacion total), st (estado committed), mp (mat).
    % Salida : sigma_out (esf. danado), d (dano), st (estado actualizado).
    E = mp.E; sy = mp.sy;
    H1 = mp.H1; g1 = mp.g1; H2 = mp.H2; g2 = mp.g2;

    % arrancar de las variables committed
    a1 = st.a1; a2 = st.a2; epl = st.epl;

    % --- predictor elastico (cpp l.341-344) ---
    dstrain = eps_new - st.eps_c;
    sigma   = st.sig_c + E*dstrain;

    % --- funcion de fluencia sobre el esf. relativo (cpp l.347-351) ---
    xi = sigma - a1 - a2;
    F  = abs(xi) - sy;

    if F > 0
        % return-mapping de Newton sobre delta_lambda (cpp l.390-423)
        delta_lambda = 0; dlambda = 0;
        for it = 1:1000
            sg = sign(xi);
            % derivada dF/dlambda (cpp lam_yield_derivative l.353-361)
            dsigma = -E*sg;
            da1    =  H1*sg - g1*a1;
            da2    =  H2*sg - g2*a2;
            dF     = sg*(dsigma - da1 - da2);
            if dF == 0, break; end
            dlambda      = -F/dF;
            delta_lambda = delta_lambda + dlambda;
            % actualizacion (cpp lam_yield_update l.363-370)
            sg     = sign(xi);
            sigma  = sigma - sg*dlambda*E;
            a1     = sg*H1/g1 - (sg*H1/g1 - st.a1)*exp(-g1*delta_lambda);
            a2     = sg*H2/g2 - (sg*H2/g2 - st.a2)*exp(-g2*delta_lambda);
            % residuo
            xi = sigma - a1 - a2;
            F  = abs(xi) - sy;
            if abs(F) < 1e-6*sy && abs(dlambda) < 1e-10
                % def. plastica POSITIVA para fractura (cpp l.414-415)
                if sg > 0
                    epl = epl + sg*delta_lambda;
                end
                break;
            end
        end
    end

    % --- estado efectivo (sin danar) committed para el proximo paso ---
    st.eps_c = eps_new;
    st.sig_c = sigma;       % esf. EFECTIVO sin danar (predictor futuro)
    st.a1 = a1; st.a2 = a2; st.epl = epl;

    % --- dano por fractura (cpp l.2256-2273) ---
    d = 0;
    if epl > mp.eupl
        sig_d = max(1e-4*sy, sy*exp(-sy*(epl - mp.eupl)/mp.G));
        d = 1 - sig_d/sy;
    end
    sigma_out = (1 - d)*sigma;    % plasticidad en espacio efectivo
end
