% Driver de MATLAB R2017a: corre los MISMOS .m del banco, uno tras otro, en una
% sola sesion (asi no se paga 6 veces el arranque de MATLAB). Cada caso imprime
% sus CHECK y su t_seg; el runner de Python parsea este log.
casos = {'b1_densa','b2_sparse','b3_bucle','b4_vector','b5_fem_q4','b6_fft'};
for cidx = 1:numel(casos)
  nombre = casos{cidx};
  fprintf('=== CASO %s\n', nombre);
  try
    run(nombre);
  catch err
    fprintf('ERROR %s\n', err.message);
  end
  clearvars -except casos cidx
end
fprintf('=== FIN\n');
