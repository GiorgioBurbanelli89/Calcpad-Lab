% Correlacion_Ks_Tecnisuelos_Portoviejo.m
% =========================================================================
% CORRELACION K_s con estudio REAL Tecnisuelos-NEC TSN-ES-2025-206
% Proyecto: Vivienda Familiar - Los Arenales, Portoviejo - Manabi
% =========================================================================
% Datos del informe (extraidos del PDF):
%   q_adm = 2.19 kg/cm² = 21.9 t/m² (a 1.00 m desplante)
%   N-SPT promedio S1 = 40, S2 = 48
%   Tipo suelo: D (NEC-SE-DS)
%   Arena mal graduada SP (0-3m), SP-SM (3-6m)
%   NF: -3.05 m (S1), no detectado (S2)
%   Sismica: Z=0.50, Zona VI, alta amenaza, licuefaccion 3-6m
%   Cimentacion recomendada: zapata aislada, desplante 1.0m
%
% Objetivo: estimar K_s por 4 metodos y comparar para diseno estructural
% =========================================================================

clear; clc;

DASH = '----------------------------------------------------------------';

% =========================================================================
% INPUTS DEL ESTUDIO GEOTECNICO REAL
% =========================================================================
q_adm = 21.9;        % t/m² (= 2.19 kg/cm²)
q_adm_kPa = q_adm * 9.80665;
N_spt = 40;          % promedio S1 (suelo arena SP/SP-SM)
nu_soil = 0.30;      % Poisson arena (suelta-media)
NF_depth = 3.05;     % nivel freatico (m bajo terreno natural)
B_zapata = 1.5;      % ancho zapata asumido (m)
L_zapata = 1.5;      % largo zapata
h_zapata = 0.40;     % espesor zapata (m)
desplante = 1.00;    % m
fc_concreto = 280;   % kg/cm²

E_concrete = 14100 * sqrt(fc_concreto) * 10;  % t/m²

fprintf('=================================================================\n');
fprintf('  CORRELACION K_s — Estudio Tecnisuelos TSN-ES-2025-206\n');
fprintf('  Vivienda Familiar - Portoviejo, Manabi\n');
fprintf('=================================================================\n\n');

fprintf('  Datos del estudio:\n');
fprintf('    q_adm = %.2f t/m² (= 2.19 kg/cm²)\n', q_adm);
fprintf('    N-SPT promedio = %.0f (arena SP/SP-SM)\n', N_spt);
fprintf('    NF = %.2f m  (CRITICO si zapata > %.2f m)\n', NF_depth, NF_depth);
fprintf('    Zapata supuesta: %.1fx%.1fx%.2fm, desplante %.2fm\n\n', ...
        B_zapata, L_zapata, h_zapata, desplante);

% =========================================================================
% METODO 1 - TABLA MORRISON / GUERRA (factor 200 alto q_adm)
% =========================================================================
fprintf('=================================================================\n');
fprintf('  METODO 1 - Tabla Morrison (Guerra MDI pag.17)\n');
fprintf('=================================================================\n\n');

% Para q_adm en region 20-40, factor K/q = 200
% Para q_adm = 21.9, interpolando entre 21 (K=4200) y 22 (K=4400)
K_morrison = interpola_morrison(q_adm);
fprintf('  K_s = tabla Morrison (q_adm=21.9 t/m²) = %.0f t/m³\n', K_morrison);
fprintf('  Implica delta_admisible = %.2f mm\n\n', q_adm/K_morrison*1000);

% =========================================================================
% METODO 2 - BOWLES (estandar Ecuador en informes profesionales)
% =========================================================================
fprintf('=================================================================\n');
fprintf('  METODO 2 - Bowles (1996): Ks = 40·SF·q_adm\n');
fprintf('=================================================================\n\n');

SF_bowles = 3;
K_bowles_t = 40 * SF_bowles * q_adm;            % kN/m³ con q en kPa
% pero q_adm aqui esta en t/m², 40·SF·q_adm en t/m³ es la misma formula
K_bowles_kN = 40 * SF_bowles * q_adm_kPa;       % kN/m³
fprintf('  K_s = 40 × %.0f × %.2f = %.0f t/m³\n', SF_bowles, q_adm, K_bowles_t);
fprintf('       = %.0f kN/m³\n\n', K_bowles_kN);

% =========================================================================
% METODO 3 - VESIC desde Es estimado por correlacion SPT
% =========================================================================
fprintf('=================================================================\n');
fprintf('  METODO 3 - Vesic 1961 con E_s estimado desde SPT\n');
fprintf('=================================================================\n\n');

% Schmertmann (1970) para arenas: E_s (kPa) = a*(N+15) con a=500 fina, 750 media
% Bowles: arena SP - SM: E_s = 250·(N+15) hasta 500·(N+15) kPa
% Conservador: a=500
a_schmer = 500;
E_s_kPa = a_schmer * (N_spt + 15);
E_s = E_s_kPa / 9.80665;     % t/m²
fprintf('  Correlacion Schmertmann: E_s = %.0f × (%.0f+15) = %.0f kPa\n', ...
        a_schmer, N_spt, E_s_kPa);
fprintf('  E_s = %.0f t/m²\n\n', E_s);

% Vesic 1961: K_s = (0.65/(B(1-nu²))) * (Es·B^4/(Ec·If))^(1/12) * Es
I_zapata = B_zapata * h_zapata^3 / 12;
ratio = (E_s * B_zapata^4) / (E_concrete * I_zapata);
factor_root = ratio^(1/12);
K_vesic = (0.65 / (B_zapata * (1 - nu_soil^2))) * factor_root * E_s;
fprintf('  Vesic: K_s = (0.65/B·(1-nu²)) · (Es·B^4/(Ec·If))^(1/12) · Es\n');
fprintf('         = %.3f × %.3f × %.0f = %.0f t/m³\n\n', ...
        0.65/(B_zapata*(1-nu_soil^2)), factor_root, E_s, K_vesic);

% =========================================================================
% METODO 4 - REDUCCION por NIVEL FREATICO (NF a 3.05m)
% =========================================================================
fprintf('=================================================================\n');
fprintf('  METODO 4 - Correccion por nivel freatico\n');
fprintf('=================================================================\n\n');

% NF a 3.05m del terreno natural; cimentacion a 1.00m desplante
% Zona de influencia = B = 1.5m, asi llega solo a 1+1.5 = 2.5m
% NF esta debajo de zona influencia → reduccion menor (30%)
% Si zapata fuera mas grande, mayor influencia
zona_influencia = desplante + B_zapata;
if NF_depth < zona_influencia
    reduccion_NF = 0.50;   % NF en zona activa → reduccion 50%
    fprintf('  NF=%.2fm DENTRO zona influencia (%.2fm) → reducir K_s 50%%\n', ...
            NF_depth, zona_influencia);
else
    reduccion_NF = 0.70;   % NF debajo zona activa → reduccion 30%
    fprintf('  NF=%.2fm DEBAJO zona influencia (%.2fm) → reducir K_s 30%%\n', ...
            NF_depth, zona_influencia);
end

K_morrison_NF = K_morrison * reduccion_NF;
K_bowles_NF = K_bowles_t * reduccion_NF;
K_vesic_NF = K_vesic * reduccion_NF;

fprintf('\n  K_s corregido por NF (factor %.2f):\n', reduccion_NF);
fprintf('    Morrison:  %.0f → %.0f t/m³\n', K_morrison, K_morrison_NF);
fprintf('    Bowles:    %.0f → %.0f t/m³\n', K_bowles_t, K_bowles_NF);
fprintf('    Vesic:     %.0f → %.0f t/m³\n', K_vesic, K_vesic_NF);

% =========================================================================
% TABLA RESUMEN
% =========================================================================
fprintf('\n=================================================================\n');
fprintf('  RESUMEN COMPARATIVO K_s\n');
fprintf('=================================================================\n\n');

fprintf('  Metodo               K_s sin NF    K_s con NF    Comentario\n');
fprintf('  %s\n', DASH);
fprintf('  Morrison (tabla)     %5.0f t/m³    %5.0f t/m³    Optimista (no B)\n', K_morrison, K_morrison_NF);
fprintf('  Bowles (40·SF·q)     %5.0f t/m³    %5.0f t/m³    Estandar Ecuador\n', K_bowles_t, K_bowles_NF);
fprintf('  Vesic (Es desde SPT) %5.0f t/m³    %5.0f t/m³    Realista, B=%.1fm\n', K_vesic, K_vesic_NF, B_zapata);
fprintf('  %s\n\n', DASH);

% Promedio y desviacion
K_vals = [K_morrison_NF, K_bowles_NF, K_vesic_NF];
K_avg = mean(K_vals);
K_std = std(K_vals);
K_min = min(K_vals);

fprintf('  K_s promedio (con NF):    %.0f t/m³\n', K_avg);
fprintf('  K_s desviacion estandar:  %.0f t/m³ (%.1f%%)\n', K_std, K_std/K_avg*100);
fprintf('  K_s MINIMO (conservador): %.0f t/m³ ← RECOMENDADO\n\n', K_min);

% =========================================================================
% VERIFICACION ASENTAMIENTO ELASTICO
% =========================================================================
fprintf('=================================================================\n');
fprintf('  VERIFICACION ASENTAMIENTO ELASTICO (Boussinesq)\n');
fprintf('=================================================================\n\n');

% Se = q·B·(1-nu²)/Es · If    (zapata flexible)
% If para zapata cuadrada flexible ~ 1.12 (Bowles tab.5-4)
If_zapata = 1.12;
Se_elastic = q_adm * B_zapata * (1 - nu_soil^2) / E_s * If_zapata;
Se_elastic_mm = Se_elastic * 1000;
fprintf('  S_e = q·B·(1-nu²)/Es · If\n');
fprintf('      = %.1f × %.1f × %.2f / %.0f × %.2f\n', ...
        q_adm, B_zapata, 1-nu_soil^2, E_s, If_zapata);
fprintf('      = %.4f m = %.2f mm\n', Se_elastic, Se_elastic_mm);

Se_admisible = 25;   % mm (limite NEC tipico zapata aislada)
if Se_elastic_mm > Se_admisible
    fprintf('\n  ⚠ Se = %.1f mm > %.0f mm admisible → REVISAR\n', Se_elastic_mm, Se_admisible);
    fprintf('    Opciones: ampliar zapata, mejorar suelo, o losa cimentacion\n');
else
    fprintf('\n  ✓ Se = %.1f mm < %.0f mm admisible → OK\n', Se_elastic_mm, Se_admisible);
end

% =========================================================================
% RECOMENDACION FINAL PARA EL ESTRUCTURAL
% =========================================================================
fprintf('\n=================================================================\n');
fprintf('  RECOMENDACION FINAL PARA EL DISEÑO ESTRUCTURAL\n');
fprintf('=================================================================\n\n');

fprintf('  Para zapata aislada %.1fx%.1f m, desplante %.2fm:\n\n', ...
        B_zapata, L_zapata, desplante);
fprintf('    q_adm  = %.1f t/m² (del estudio)\n', q_adm);
fprintf('    K_s    = %.0f t/m³ (conservador entre 3 metodos)\n', K_min);
fprintf('    S_e    = %.2f mm (verificacion)\n', Se_elastic_mm);
fprintf('    NF     = %.2f m (afecta K_s, reducido 50%%)\n\n', NF_depth);

fprintf('  Sensitivity analysis recomendada en Hekatan/SAP/ETABS:\n');
fprintf('    Caso conservador: K_s = %.0f t/m³\n', round(K_min*0.7));
fprintf('    Caso esperado:    K_s = %.0f t/m³\n', round(K_avg));
fprintf('    Caso optimista:   K_s = %.0f t/m³\n', round(max(K_vals)));

fprintf('\n=================================================================\n');
fprintf('  Fin del analisis.\n');
fprintf('=================================================================\n');

% =========================================================================
% FUNCION: interpolacion lineal en tabla Morrison
% =========================================================================
function K = interpola_morrison(qa)
    % Datos tabla Morrison (Guerra MDI pag.17)
    Q = [2.5 5 10 15 20 25 30 35 40];
    K_tab = [650 1300 2200 3100 4000 5000 6000 7000 8000];
    if qa <= Q(1)
        K = K_tab(1);
    elseif qa >= Q(end)
        K = K_tab(end);
    else
        K = interp1(Q, K_tab, qa, 'linear');
    end
end
