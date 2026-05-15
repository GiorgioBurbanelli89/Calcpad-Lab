%% Calcpad Lab — FEM intrínseca via hekatan-fem C++/Eigen
% Demuestra el bloque #fem que llama al kernel C++ (Eigen) del proyecto hekatan-fem.
% Soporta los 6 casos validados: plate_thin, plate_thick, membrane, layered, shell_thin, shell_thick.

%% Plate Thin — solver FEM C++ MITC4/DKMQ
% Geometría: 1×1×0.05, q=1, malla 4×4, SS
#fem plate_thin
#end fem

%% Plate Thick — Mindlin-Reissner
#fem plate_thick
#end fem

%% Membrane (plane stress) cantilever 5×3, P=100
#fem membrane
#end fem

%% Shell Thin cantilever 1×1×0.005, P=1
#fem shell_thin
#end fem

%% Shell Thick cantilever 0.5×0.5×0.05, P=100
#fem shell_thick
#end fem

%% Layered [0/90/90/0] iso, 4 capas × 0.05
#fem layered
#end fem

%% Notas
% Los valores arriba son del solver C++/Eigen (hekatan-fem MITC4/DKMQ).
% Cada llamada toma ~200-500ms (overhead Node.js + JIT del WASM).
% Próxima versión: P/Invoke nativo → <10ms por llamada.
