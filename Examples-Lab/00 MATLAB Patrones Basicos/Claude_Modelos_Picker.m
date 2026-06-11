% Claude_Modelos_Picker.m
% =========================================================================
% EXPLICACION del menu de seleccion de Claude (Modelos / Esfuerzo / Modo)
% =========================================================================
% Documenta las opciones del picker que aparece con Shift+Ctrl+I (modelos)
% o Shift+Ctrl+E (esfuerzo) en Claude Code y otras interfaces Anthropic.
%
% Ejecutar:
%   - MATLAB: F5 desde editor
%   - Calcpad-Lab CLI: CalcpadLabCli.exe Claude_Modelos_Picker.m
% =========================================================================

clear; clc;

DASH = '----------------------------------------------------------------';

% =========================================================================
% SECCION 1 - MODELOS DISPONIBLES (Shift+Ctrl+I)
% =========================================================================

fprintf('=================================================================\n');
fprintf('  MODELOS DISPONIBLES EN CLAUDE\n');
fprintf('=================================================================\n\n');

% Datos comparativos (publicos Anthropic).
% Columnas: Capa, Vel tok/s, CtxK, CostIn $/M, CostOut $/M
%           [1=basica .. 10=top]

fprintf('  Modelo               Capa  Tok/s   Ctx   $In/M  $Out/M\n');
fprintf('  %s\n', DASH);
fprintf('  Opus 4.7              10    60    200K   15    75\n');
fprintf('  Opus 4.7 1M           10    55   1000K   15    75\n');
fprintf('  Sonnet 4.6             8   100    200K    3    15\n');
fprintf('  Haiku 4.5              6   180    200K    1     5\n');
fprintf('  Opus 4.6 Legado        9    70    200K   15    75\n\n');

fprintf('  NOTA: Opus 4.7 1M cobra 2x si prompts > 200K tokens:\n');
fprintf('        <= 200K: $15 in / $75 out (igual que Opus 4.7)\n');
fprintf('        >  200K: $30 in / $150 out por millon de tokens\n\n');

% =========================================================================
% SECCION 2 - NIVELES DE ESFUERZO (Shift+Ctrl+E)
% =========================================================================

fprintf('=================================================================\n');
fprintf('  NIVELES DE ESFUERZO (THINKING TOKENS)\n');
fprintf('=================================================================\n\n');

fprintf('  Mas esfuerzo = mas razonamiento interno = mejor calidad,\n');
fprintf('  mas lento, mas costo (los thinking tokens se cobran).\n\n');

fprintf('  Nivel        Tokens    Uso tipico\n');
fprintf('  %s\n', DASH);
fprintf('  Baja          2048    Q&A simple, lookups, codigo trivial\n');
fprintf('  Medio         8192    Refactor moderado, debug, edits\n');
fprintf('  Alto         16384    Arquitectura, multi-step planning\n');
fprintf('  Extra alto   32768    FEM, math derivations, investigacion\n');
fprintf('  Max          65536    Hard reasoning, pruebas matematicas\n\n');

fprintf('  IMPORTANTE: thinking tokens NO se ven en la respuesta final\n');
fprintf('  pero SI se cobran como tokens de output a precio normal.\n\n');

% =========================================================================
% SECCION 3 - MODO RAPIDO
% =========================================================================

fprintf('=================================================================\n');
fprintf('  MODO RAPIDO\n');
fprintf('=================================================================\n\n');

fprintf('  Toggle on/off via menu. Cuando activado:\n');
fprintf('   - Streaming mas agresivo (chunks mayores)\n');
fprintf('   - Latencia inicial -30%% (time-to-first-token)\n');
fprintf('   - Prioriza cache hits sobre prompts repetidos\n');
fprintf('   - Conviene cuando thinking esta en Baja/Medio\n');
fprintf('   - NO cambia la calidad del modelo subyacente\n\n');

% =========================================================================
% SECCION 4 - CALCULO COSTO SESION TIPICA HEKATAN-STRUCT
% =========================================================================

fprintf('=================================================================\n');
fprintf('  EJEMPLO: COSTO SESION TIPICA HEKATAN-STRUCT\n');
fprintf('=================================================================\n\n');

% Sesion debugeando FEM example. Tokens acumulados.
ctx_input_total      = 500000;   % tokens input acumulado
ctx_cache_hit_ratio  = 0.80;
ctx_output           =  50000;
ctx_thinking         =  20000;

ctx_input_billable = ctx_input_total * (1 - ctx_cache_hit_ratio);
ctx_cached         = ctx_input_total * ctx_cache_hit_ratio;

% Precios Opus 4.7 1M (tier >200K) USD por millon tokens
P_in_full   = 30;
P_in_cached =  3;     % 10% del costo input
P_out       = 150;    % output + thinking

costo_input = (ctx_input_billable / 1e6) * P_in_full;
costo_cache = (ctx_cached         / 1e6) * P_in_cached;
costo_out   = (ctx_output         / 1e6) * P_out;
costo_think = (ctx_thinking       / 1e6) * P_out;
costo_total = costo_input + costo_cache + costo_out + costo_think;

fprintf('  Config: Opus 4.7 1M, Extra alto, Modo Rapido ON\n\n');

fprintf('  Tokens billable INPUT (no cache):     %8.0f   $ %6.3f\n', ctx_input_billable, costo_input);
fprintf('  Tokens cached INPUT (10%% precio):    %8.0f   $ %6.3f\n', ctx_cached, costo_cache);
fprintf('  Tokens OUTPUT respuesta:              %8.0f   $ %6.3f\n', ctx_output, costo_out);
fprintf('  Tokens THINKING (oculto):             %8.0f   $ %6.3f\n', ctx_thinking, costo_think);
fprintf('  %s\n', DASH);
fprintf('  TOTAL sesion Opus 4.7 1M:                       $ %6.3f USD\n\n', costo_total);

% Comparativa misma sesion en otros modelos
fprintf('  Misma sesion en otros modelos:\n');

% Opus 4.7 (no 1M, asumiendo <200K = mismo precio menor)
P_in = 15; P_out_x = 75;
c1 = (ctx_input_billable/1e6)*P_in + (ctx_cached/1e6)*(P_in*0.1) + (ctx_output/1e6)*P_out_x + (ctx_thinking/1e6)*P_out_x;
fprintf('    Opus 4.7 (<=200K)     $ %6.3f USD\n', c1);

% Sonnet 4.6
P_in = 3; P_out_x = 15;
c2 = (ctx_input_billable/1e6)*P_in + (ctx_cached/1e6)*(P_in*0.1) + (ctx_output/1e6)*P_out_x + (ctx_thinking/1e6)*P_out_x;
fprintf('    Sonnet 4.6            $ %6.3f USD\n', c2);

% Haiku 4.5
P_in = 1; P_out_x = 5;
c3 = (ctx_input_billable/1e6)*P_in + (ctx_cached/1e6)*(P_in*0.1) + (ctx_output/1e6)*P_out_x + (ctx_thinking/1e6)*P_out_x;
fprintf('    Haiku 4.5             $ %6.3f USD\n\n', c3);

% =========================================================================
% SECCION 5 - RECOMENDACION PRACTICA
% =========================================================================

fprintf('=================================================================\n');
fprintf('  RECOMENDACION FLUJO HEKATAN-STRUCT\n');
fprintf('=================================================================\n\n');

fprintf('  Modelo:      Opus 4.7 1M   (sesiones largas, codebase grande)\n');
fprintf('  Esfuerzo:    Extra alto    (FEM/estructural requiere razonar)\n');
fprintf('  Modo rapido: ON            (no baja calidad, baja latencia)\n\n');

fprintf('  Cuando bajar a Sonnet 4.6:\n');
fprintf('   - Sesion < 50K tokens (no necesitas 1M)\n');
fprintf('   - Tareas rutinarias (commits, formato, lookups)\n');
fprintf('   - Ahorro 5x input, 5x output\n\n');

fprintf('  Cuando bajar Esfuerzo a Medio:\n');
fprintf('   - Aplicar parches conocidos\n');
fprintf('   - Generar boilerplate\n');
fprintf('   - Buscar archivos (no razonar)\n\n');

fprintf('=================================================================\n');
fprintf('  Fin del documento.\n');
fprintf('=================================================================\n');
