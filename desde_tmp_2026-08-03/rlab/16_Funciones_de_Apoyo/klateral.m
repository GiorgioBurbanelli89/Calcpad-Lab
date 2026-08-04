function [X, Y, NI, NJ, CG, SS, kc] = klateral(sv, sp, Lvi, Lvd, E, Areag, Inerciag, cc1, cc2, beta, Iag, v)
    % --- Código de geometria_volcar ---
    nv = length(sv);      % Calcula el número de vanos
    np = length(sp);      % Calcula el número de pisos
    nr = nv + 1;          % Calcula el número de nudos restringidos
    nudcol = (np) * (nv + 1); % Calcula el número de columnas
    nudvg = nv * np;      % Calcula el número de vigas centrales (SIN VOLADOS)
    if (Lvi == 0 && Lvd ~= 0) || (Lvd == 0 && Lvi ~= 0)
        nod = (nv + 2) * np + nr;
        nudnmc = np;
    elseif Lvi == 0 && Lvd == 0
        nod = (nv + 1) * np + nr;
        nudnmc = 0;
    else
        nod = (nv + 3) * (np) + nr; 
        nudnmc = 2 * np;
    end
    nudt = nudcol + nudvg + nudnmc;  % Calcula el número de elementos totales

    % --- Código de glinea_portico_volcar ---
    xa = zeros(nv + 1, 1);
    for i = 1: nv + 1
        if i == 1
            xa(i, 1) = 0;
        else
            xa(i, 1) = sv(i - 1, 1) + xa(i - 1, 1);
        end
    end

    ya = zeros(np + 1, 1);
    for i = 1: np + 1
        if i == 1
            ya(i, 1) = 0;
        else
            ya(i, 1) = sp(i - 1, 1) + ya(i - 1, 1);
        end
    end

    nud = zeros(nod, 2);
    for j = 1:nr
        if j == 1
            nud(j, 1) = 0;
        else
            nud(j, 1) = sv(j - 1) + nud(j - 1, 1);
        end
    end
    r = nr + 1;

    for i = 1:np
        for j = 1:nv + 1
            nud(r, 1) = xa(j, 1);
            nud(r, 2) = ya(i + 1);
            r = r + 1;
        end
    end

    if Lvi == 0 && Lvd == 0
        X = nud(:, 1)';
        Y = nud(:, 2)';
    elseif (Lvi == 0 && Lvd ~= 0) || (Lvd == 0 && Lvi ~= 0)
        for i = 1:np
            if Lvi == 0
                nud(r, 1) = xa(end, 1) + Lvd;
            else
                nud(r, 1) = 0 - Lvi;
            end
            nud(r, 2) = ya(i + 1);
            r = r + 1;
        end
    else
        for i = 1:np
            for j = 1:2
                if j == 1
                    nud(r, 1) = 0 - Lvi;
                else
                    nud(r, 1) = xa(end, 1) + Lvd;
                end
                nud(r, 2) = ya(i + 1);
                r = r + 1;
            end
        end
    end

    X = nud(:, 1)';
    Y = nud(:, 2)';
    X = X + Lvi;

    % --- Código de gn_portico_volcar ---
    NI = zeros(1, nudt);
    NJ = zeros(1, nudt);

    for i = 1:nudcol
        NI(1, i) = i;
        NJ(1, i) = NI(1, i) + nr;
    end

    p = 1;
    s = 1;
    i = 1 + nudcol;

    for j = 1:nudvg   
        NI(1, i) = (nr) * p + s;
        NJ(1, i) = NI(1, i) + 1;
        i = i + 1;
        s = s + 1;
        if s > nv
            s = 1;
            p = p + 1;
        end
    end

    nii = NJ(i - 1);
    p = 1;
    s = 1;
    i = 1 + nudcol + nudvg;

    if Lvi == 0 && Lvd == 0
        % Do nothing
    elseif (Lvi == 0 && Lvd ~= 0) || (Lvd == 0 && Lvi ~= 0)
        for j = 1:nudnmc
            if Lvd == 0
                NI(1, i) = nii + 1;
                NJ(1, i) = nr * p + s;
            else
                NI(1, i) = nr * p + s + nv;
                NJ(1, i) = nii + 1;
            end
            i = i + 1;
            p = p + 1;
            nii = nii + 1;
        end
    else
        for j = 1:nudnmc / 2
            NI(1, i) = nii + 1;
            NJ(1, i) = (nr) * p + s;
            NI(1, i + 1) = NJ(1, i) + nv;
            NJ(1, i + 1) = NI(1, i) + 1;
            i = i + 2;
            p = p + 1;
            nii = nii + 2;
        end
    end

    % --- Código de cg_sismo2 ---
    CG = zeros(nod, 3);
    [~, ~, CG(:, 1)] = unique(Y);
    CG(:, 1) = CG(:, 1) - ones(nod, 1);

    CG2 = zeros(2, nod - nr);
    ngl = max(CG(:, 1));
    for i = 1:(nod - nr) * 2
        ngl = ngl + 1;
        CG2(i) = ngl;
    end
    CG(nr + 1:size(CG, 1), 2:3) = CG2';

    % --- Código de longitud ---
    mbr = length(NI);
    L = zeros(1, mbr);
    seno = zeros(1, mbr);
    coseno = zeros(1, mbr);
    for i = 1:mbr
        dx = X(NJ(i)) - X(NI(i));
        dy = Y(NJ(i)) - Y(NI(i));
        L(i) = sqrt(dx * dx + dy * dy);
        seno(i) = dy / L(i);
        coseno(i) = dx / L(i);
    end

    % --- Código de vc ---
    mbr = length(NI); 
    icod = length(CG(1, :)); 
    VC = zeros(mbr, icod);
    for i = 1:mbr
        for j = 1:icod
            VC(i, j) = CG(NI(i), j); 
            VC(i, j + icod) = CG(NJ(i), j);
        end
    end

    % --- Código de krigidez_nudo_rigido_compuesta ---
    SS = zeros(ngl);
    kc = cell(mbr, 1);  % Inicializar kc como una celda para almacenar las matrices de rigidez

    for i = 1: mbr
        if icod == 4
            Area = Areag(i);  % Área del elemento
            Lon = L(i);
            sen = seno(i);
            cose = coseno(i);
            % kdiagonal function code
            k = E * Area / Lon * [cose^2, cose * sen, -cose^2, -cose * sen;
                                  cose * sen, sen^2, -cose * sen, -sen^2;
                                  -cose^2, -cose * sen, cose^2, cose * sen;
                                  -cose * sen, -sen^2, cose * sen, sen^2];
        else
            Lon = L(i);
            sen = seno(i);
            cose = coseno(i);
            Area = Areag(i);  % Área del elemento
            Inercia = Inerciag(i);  % Inercia gruesa del elemento
            c1 = cc1(i);  % Longitud del nudo rígido del nudo inicial
            c2 = cc2(i);  % Longitud del nudo rígido del nudo final
            fa = Iag(i);  % Factor de agrietamiento

            % kmiembro_nudo_rigido_compuesta function code
            G = E / (2 * (1 + v));  % Módulo de rigidez
            Iagr = fa * Inercia;  % Inercia ajustada

            % Coeficiente de rigidez
            fi = (3 * E * Iagr * beta) / (G * Area * Lon^2);
            kf = (4 * E * Iagr * (1 + fi)) / (Lon * (1 + 4 * fi));
            a = (2 * E * Iagr * (1 - 2 * fi)) / (Lon * (1 + 4 * fi));
            b = (kf + a) / Lon;
            t = 2 * b / Lon;
            r = E * Area / Lon;

            if sen == 0 % Caso de viga
                k = [0, 0, 0, 0, 0, 0;
                     0, t, b + c1 * t, 0, -t, b + c2 * t;
                     0, b + c1 * t, kf + 2 * c1 * b + c1^2 * t, 0, -(b + c1 * t), a + c1 * b + c2 * b + c1 * c2 * t;
                     0, 0, 0, 0, 0, 0;
                     0, -t, -(b + c1 * t), 0, t, -(b + c2 * t);
                     0, b + c2 * t, a + c1 * b + c2 * b + c1 * c2 * t, 0, -(b + c2 * t), kf + 2 * c2 * b + c2^2 * t];
            else % Caso general
                k = [t, 0, -(b + c1 * t), -t, 0, -(b + c2 * t);
                     0, r, 0, 0, -r, 0;
                     -(b + c1 * t), 0, kf + 2 * c1 * b + c1^2 * t, b + c1 * t, 0, a + c1 * b + c2 * b + c1 * c2 * t;
                     -t, 0, b + c1 * t, t, 0, b + c2 * t;
                     0, -r, 0, 0, r, 0;
                     -(b + c2 * t), 0, a + c1 * b + c2 * b + c1 * c2 * t, b + c2 * t, 0, kf + 2 * c2 * b + c2^2 * t];
            end

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
