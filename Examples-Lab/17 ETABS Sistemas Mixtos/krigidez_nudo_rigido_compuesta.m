function [SS, kc] = krigidez_nudo_rigido_compuesta(ngl, Areag, Inerciag, cc1, cc2, L, seno, coseno, VC, E, Iag, beta,v)
    % Programa para encontrar la matriz de rigidez de un pórtico plano
    % o de una armadura plana
    %
    % Por: Roberto Aguiar Falconi
    % CEINCI-ESPE
    % Noviembre de 2011
    % Modificado por: Roberto Gilces
    % Noviembre de 2022
    %-------------------------------------------------------------
    % [SS, kc] = krigidez_nudo_rigido_compuesta()
    %-------------------------------------------------------------
    % ngl: Número de grados de libertad
    % Areag: Vector con el área de los elementos de armadura
    % Inerciag: Vector con la inercia de los elementos de pórtico plano
    % cc1: Vector con longitud del nudo inicial de cada elemento
    % cc2: Vector con longitud del nudo final de cada elemento
    % L: Vector que contiene la luz libre de los elementos
    % seno: Vector que contiene los senos de los elementos
    % coseno: Vector que contiene los cosenos de los elementos
    % VC: Matriz que contiene los vectores de colocación de elementos
    % E: Módulo de elasticidad del material
    % Iag: Factor de agrietamiento relativo
    % beta: Factor de corrección
    % SS: Matriz de rigidez de la estructura
    % kc: Celdas para almacenar las matrices de rigidez de los elementos

    % Inicialización de la matriz de rigidez global
    mbr = length(L);
    SS = zeros(ngl);
    icod = size(VC, 2);
    kc = cell(mbr, 1);  % Inicializar kc como una celda para almacenar las matrices de rigidez

    for i = 1: mbr
        if icod == 4
            Area = Areag(i);  % Área del elemento
            Lon = L(i);
            sen = seno(i);
            cose = coseno(i);
            k = kdiagonal(Area, Lon, E, sen, cose);
        else
            Lon = L(i);
            sen = seno(i);
            cose = coseno(i);
            Area = Areag(i);  % Área del elemento
            Inercia = Inerciag(i);  % Inercia gruesa del elemento
            c1 = cc1(i);  % Longitud del nudo rígido del nudo inicial
            c2 = cc2(i);  % Longitud del nudo rígido del nudo final
            fa = Iag(i);  % Factor de agrietamiento

            % Llamada a kmiembro_nudo_rigido_compuesta
            k = kmiembro_nudo_rigido_compuesta(Area, Inercia, c1, c2, Lon, E, sen, cose, beta, fa,v);
            
            % Almacenar la matriz k en una celda de kc
            kc{i} = k;
        end

        for j = 1: icod
            jj = VC(i, j);
            if jj == 0
                continue
            end
            for m = 1: icod
                mm = VC(i, m);
                if mm == 0
                    continue
                end
                SS(jj, mm) = SS(jj, mm) + k(j, m);
            end
        end
    end
    
end