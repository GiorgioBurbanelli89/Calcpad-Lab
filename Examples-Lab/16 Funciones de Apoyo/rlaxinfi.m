function [KL, X, Y, NI, NJ, CG] = rlaxinfi(B, H, sv, sp, Lvi, Lvd, E)
    % Programa para encontrar la matriz de rigidez lateral de un portico plano
    % considerando que todos los elementos son axialmente rigidos.
    % Por: Roberto Aguiar Falconi
    % CEINCI-ESPE

    % ********** DATOS ********** %
    % sv:     Vector que contiene la longitud de cada vano CENTRAL (NO VOLADOS)
    % sp:     Vector que contiene la altura de cada piso
    % Lvi:    Longitud del volado - lado izquierdo
    % Lvd:    Longitud del volado - lado derecho
    % ********* REPORTA ********* %
    % nv:     Número de vanos
    % np:     Número de pisos
    % nudt:   Número de elementos totales
    % nudcol: Número de columnas
    % nudvg:  Número de vigas
    % nudnmc: Número de volados (vigas) acarteladas
    % nod:    Número de nudos
    % nr:     Número de nudos restringidos

    % Cálculos de geometría sección central (SIN VOLADOS)
    nv = length(sv);          % Calcula el número de vanos
    np = length(sp);          % Calcula el número de pisos
    nr = nv + 1;              % Calcula el número de nudos restringidos
    nudcol = (np) * (nv + 1); % Calcula el número de columnas
    nudvg = nv * np;          % Calcula el número de vigas centrales (SIN VOLADOS)
    
    % Cálculo del número de nudos y volados acartelados
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

    % Generar las coordenadas X e Y de los nudos
    xa = zeros(nv + 1, 1);
    for i = 1:nv + 1
        if i == 1
            xa(i, 1) = 0;
        else
            xa(i, 1) = sv(i - 1, 1) + xa(i - 1, 1);
        end
    end

    ya = zeros(np + 1, 1);
    for i = 1:np + 1
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

    % Vectores X e Y de los VOLADOS ACARTELADOS
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
        X = nud(:, 1)';
        Y = nud(:, 2)';
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
        X = nud(:, 1)';
        Y = nud(:, 2)';
    end
    X = X + Lvi;

    % Generar el Nudo Inicial y Final de los elementos
    NI = zeros(1, nudt);
    NJ = zeros(1, nudt);
    
    % Nudos inicial y final del tramo central (SIN CONSIDERAR VOLADOS)
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
    
    % Acoplamiento de los volados
    nii = NJ(i - 1);
    p = 1;
    s = 1;
    i = 1 + nudcol + nudvg;

    if Lvi == 0 && Lvd == 0
        % No hay volados, continuar sin cambios
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
            NJ(1, i) = nr * p + s;
            NI(1, i + 1) = NJ(1, i) + nv;
            NJ(1, i + 1) = NI(1, i) + 1;
            i = i + 2;
            p = p + 1;
            nii = nii + 2;
        end
    end

    % Coordenadas Generalizadas
    CG = zeros(nod, 2);
    ngl = 0;
    k = nr;
    for i = 1:np
        ngl = ngl + 1;
        for j = 1:nr
            k = k + 1;
            CG(k, 2) = ngl;
        end
    end

    for i = 1:nod - nr
        ngl = ngl + 1;
        k = nr + i;
        CG(k, 2) = ngl;
    end

    % Vectores de colocacion
    mbr = length(NI);
    icod = length(CG(1, :));
    VC = zeros(mbr, icod * 2);
    for i = 1:mbr
        for j = 1:icod
            VC(i, j) = CG(NI(i), j);
            VC(i, j + icod) = CG(NJ(i), j);
        end
    end

    % Calcular longitud, seno y coseno de los elementos
    L = zeros(1, mbr);
    seno = zeros(1, mbr);
    coseno = zeros(1, mbr);
    for i = 1:mbr
        dx = X(NJ(i)) - X(NI(i));
        dy = Y(NJ(i)) - Y(NI(i));
        L(i) = sqrt(dx^2 + dy^2);
        seno(i) = dy / L(i);
        coseno(i) = dx / L(i);
    end

    % Matriz de rigidez de miembro y de la estructura
    fprintf('\n Calculo con: Inercias gruesas, codigo=0. Con inercias agrietadas, codigo=1');
    icod = input('\n Ingrese codigo de inercias :');
    SS = zeros(ngl, ngl);
    for i = 1:length(B)
        b = B(i);
        h = H(i);
        long = L(i);
        iner = b * h^3 / 12;
        ei = E * iner;
        if icod == 1
            iner = 0.5 * iner;
            ei = E * iner;
        end
        k = zeros(4, 4);
        k(1, 1) = 12 * ei / long^3;
        k(1, 2) = 6 * ei / long^2;
        k(1, 3) = -k(1, 1);
        k(1, 4) = k(1, 2);
        k(2, 2) = 4 * ei / long;
        k(2, 3) = -k(1, 2);
        k(2, 4) = 2 * ei / long;
        k(3, 3) = k(1, 1);
        k(3, 4) = -k(1, 2);
        k(4, 4) = k(2, 2);
        k = k + k';

        for j = 1:4
            jj = VC(i, j);
            if jj == 0
                continue
            end
            for m = 1:4
                mm = VC(i, m);
                if mm == 0
                    continue
                end
                SS(jj, mm) = SS(jj, mm) + k(j, m);
            end
        end
    end

    % Matriz de rigidez lateral
    na = np;
    nb = ngl - np;
    Kaa = SS(1:na, 1:na);
    Kab = SS(1:na, na + 1:ngl);
    Kba = Kab';
    Kbb = SS(na + 1:ngl, na + 1:ngl);
    KL = Kaa - Kab * inv(Kbb) * Kba;
    fprintf('\n Matriz de rigidez lateral :');
    save KL KL

    % Llamar a las funciones de dibujo con los datos calculados
    dibujoplano(X, Y, NI, NJ);
    dibujogdl_new(X, Y, NI, NJ, CG);
end
