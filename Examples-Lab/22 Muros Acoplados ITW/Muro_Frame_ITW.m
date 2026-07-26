% ============================================================================
%  MURO + VIGA EN VOLADIZO  (membrana ITW con GDL drilling rz)
%  El momento de la viga entra al muro por el drilling. Sin rz la union
%  seria rotula = mecanismo. Caso clave del paper ITW-1990.
%  Traduccion fiel de Muro_Frame_ITW.cpd (Calcpad) -> Hekatan Lab / MATLAB.
% ============================================================================
tic;

% ---------- datos ----------
E  = 24850000;  nu = 0.20;  t = 0.25;
W  = 2;   H = 4;   n_x = 4;  n_z = 8;
E_b = 24850000;  b_b = 0.25;  h_b = 0.50;  L_b = 2;  N_B = 5;  P_v = 50;
n = 3;
A_b = b_b*h_b;   I_b = b_b*h_b^3/12;

% ---------- malla del muro + viga ----------
n_jw = (n_x+1)*(n_z+1);
n_j  = n_jw + N_B;
x_j = zeros(n_j,1);  y_j = zeros(n_j,1);
for j_b = 0:n_z
    for i_a = 0:n_x
        k = j_b*(n_x+1) + i_a + 1;
        x_j(k) = i_a*W/n_x;
        y_j(k) = j_b*H/n_z;
    end
end
corner = n_z*(n_x+1) + n_x + 1;
for kk = 1:N_B
    k = n_jw + kk;
    x_j(k) = W + kk*L_b/N_B;
    y_j(k) = H;
end

% ---------- conectividad muro (Q4) + nudos de la viga ----------
n_ew = n_x*n_z;
e_j = zeros(n_ew,4);
for j_b = 0:n_z-1
    for i_a = 0:n_x-1
        e = j_b*n_x + i_a + 1;
        e_j(e,1) = j_b*(n_x+1) + i_a + 1;
        e_j(e,2) = j_b*(n_x+1) + i_a + 2;
        e_j(e,3) = (j_b+1)*(n_x+1) + i_a + 2;
        e_j(e,4) = (j_b+1)*(n_x+1) + i_a + 1;
    end
end
b_n = zeros(N_B+1,1);
b_n(1) = corner;
for kk = 1:N_B
    b_n(kk+1) = n_jw + kk;
end

% ---------- constitutiva + Gauss + naturales ----------
rn = [-1 1 1 -1];   sn = [-1 -1 1 1];
D  = E*t/(1-nu^2) * [1 nu 0; nu 1 0; 0 0 (1-nu)/2];
g_p = [-sqrt(3/5); 0; sqrt(3/5)];
g_w = [5/9; 8/9; 5/9];
gam = E/(2*(1+nu));
n_g = n*n_j;
K = zeros(n_g,n_g);

% ---------- ENSAMBLAJE muro: elemento membrana ITW (drilling) ----------
for e = 1:n_ew
    q = e_j(e,:)';
    X4 = x_j(q);  Y4 = y_j(q);
    kc_arr = [2 3 4 1];
    cx_e = zeros(4,1);  cy_e = zeros(4,1);
    for ed = 1:4
        kc = kc_arr(ed);
        cx_e(ed) =  (Y4(kc)-Y4(ed))/8;
        cy_e(ed) = -(X4(kc)-X4(ed))/8;
    end
    K14 = zeros(14,14);
    dJ0 = 0;
    dNx0=zeros(4,1); dNy0=zeros(4,1); gt20=zeros(4,1); gt30=zeros(4,1);
    NN0=zeros(4,1);  dNBx0=0; dNBy0=0;
    for i_g = 1:3
        for j_g = 1:3
            rr = g_p(i_g);  ss = g_p(j_g);  ww = g_w(i_g)*g_w(j_g);
            dr = zeros(4,1); ds = zeros(4,1); NN = zeros(4,1);
            for ii = 1:4
                dr(ii) = 0.25*rn(ii)*(1 + sn(ii)*ss);
                ds(ii) = 0.25*sn(ii)*(1 + rn(ii)*rr);
                NN(ii) = 0.25*(1 + rn(ii)*rr)*(1 + sn(ii)*ss);
            end
            J11 = dr'*X4;  J12 = dr'*Y4;  J21 = ds'*X4;  J22 = ds'*Y4;
            dJ  = J11*J22 - J12*J21;
            Ji11 = J22/dJ;  Ji12 = -J12/dJ;  Ji21 = -J21/dJ;  Ji22 = J11/dJ;
            dNx = zeros(4,1); dNy = zeros(4,1); NSx = zeros(4,1); NSy = zeros(4,1);
            nsr_a = 0.5*[-2*rr*(1-ss); 1-ss^2; -2*rr*(1+ss); -(1-ss^2)];
            nss_a = 0.5*[-(1-rr^2); -2*ss*(1+rr); 1-rr^2; -2*ss*(1-rr)];
            for ii = 1:4
                dNx(ii) = Ji11*dr(ii) + Ji12*ds(ii);
                dNy(ii) = Ji21*dr(ii) + Ji22*ds(ii);
                NSx(ii) = Ji11*nsr_a(ii) + Ji12*nss_a(ii);
                NSy(ii) = Ji21*nsr_a(ii) + Ji22*nss_a(ii);
            end
            nbr = -2*rr*(1-ss^2);  nbs = -2*ss*(1-rr^2);
            dNBx = Ji11*nbr + Ji12*nbs;
            dNBy = Ji21*nbr + Ji22*nbs;
            gt1 = zeros(4,1); gt2 = zeros(4,1); gt3 = zeros(4,1); gt4 = zeros(4,1);
            ep_arr = [4 1 2 3];
            for ii = 1:4
                ep = ep_arr(ii);
                gt1(ii) = NSx(ep)*cx_e(ep) - NSx(ii)*cx_e(ii);
                gt2(ii) = NSy(ep)*cx_e(ep) - NSy(ii)*cx_e(ii);
                gt3(ii) = NSx(ep)*cy_e(ep) - NSx(ii)*cy_e(ii);
                gt4(ii) = NSy(ep)*cy_e(ep) - NSy(ii)*cy_e(ii);
            end
            Bm = [ ...
              dNx(1) 0 gt1(1) dNx(2) 0 gt1(2) dNx(3) 0 gt1(3) dNx(4) 0 gt1(4) dNBx 0; ...
              0 dNy(1) gt4(1) 0 dNy(2) gt4(2) 0 dNy(3) gt4(3) 0 dNy(4) gt4(4) 0 dNBy; ...
              dNy(1) dNx(1) gt2(1)+gt3(1) dNy(2) dNx(2) gt2(2)+gt3(2) dNy(3) dNx(3) gt2(3)+gt3(3) dNy(4) dNx(4) gt2(4)+gt3(4) dNBy dNBx];
            K14 = K14 + ww*dJ*(Bm'*D*Bm);
            if i_g==2 && j_g==2
                dNx0=dNx; dNy0=dNy; gt20=gt2; gt30=gt3; NN0=NN;
                dNBx0=dNBx; dNBy0=dNBy; dJ0=dJ;
            end
        end
    end
    res0 = zeros(14,1);
    for ii = 1:4
        res0(3*ii-2) = -0.5*dNy0(ii);
        res0(3*ii-1) =  0.5*dNx0(ii);
        res0(3*ii)   =  0.5*(gt30(ii)-gt20(ii)) - NN0(ii);
    end
    res0(13) = -0.5*dNBy0;
    res0(14) =  0.5*dNBx0;
    K14 = K14 + gam*t*4*dJ0*(res0*res0');
    Kbb = K14(13:14,13:14);  Kab = K14(1:12,13:14);
    Kba = K14(13:14,1:12);   Kuu = K14(1:12,1:12);
    K_e = Kuu - Kab*(Kbb\Kba);
    for i = 1:4
        for j = 1:4
            j1 = e_j(e,i);  j2 = e_j(e,j);
            i1 = n*(j1-1); i2 = n*(j2-1);
            K(i1+1:i1+3, i2+1:i2+3) = K(i1+1:i1+3, i2+1:i2+3) + ...
                K_e(n*(i-1)+1:n*i, n*(j-1)+1:n*j);
        end
    end
end

% ---------- ENSAMBLAJE viga (frame 2D) ----------
for ib = 1:N_B
    p1 = b_n(ib);  p2 = b_n(ib+1);
    dxb = x_j(p2)-x_j(p1);  dyb = y_j(p2)-y_j(p1);
    Le = sqrt(dxb^2+dyb^2);  cb = dxb/Le;  sb = dyb/Le;
    EA = E_b*A_b/Le;  EIz = E_b*I_b;
    kl = [ EA 0 0 -EA 0 0; ...
           0 12*EIz/Le^3 6*EIz/Le^2 0 -12*EIz/Le^3 6*EIz/Le^2; ...
           0 6*EIz/Le^2 4*EIz/Le 0 -6*EIz/Le^2 2*EIz/Le; ...
          -EA 0 0 EA 0 0; ...
           0 -12*EIz/Le^3 -6*EIz/Le^2 0 12*EIz/Le^3 -6*EIz/Le^2; ...
           0 6*EIz/Le^2 2*EIz/Le 0 -6*EIz/Le^2 4*EIz/Le];
    Tb = [ cb sb 0 0 0 0; -sb cb 0 0 0 0; 0 0 1 0 0 0; ...
           0 0 0 cb sb 0; 0 0 0 -sb cb 0; 0 0 0 0 0 1];
    K_b = Tb'*kl*Tb;
    nb = [p1 p2];
    for i = 1:2
        for j = 1:2
            j1 = nb(i);  j2 = nb(j);
            i1 = n*(j1-1); i2 = n*(j2-1);
            K(i1+1:i1+3, i2+1:i2+3) = K(i1+1:i1+3, i2+1:i2+3) + ...
                K_b(n*(i-1)+1:n*i, n*(j-1)+1:n*j);
        end
    end
end

% ---------- apoyo empotrado en la base (y=0) ----------
k_s = 1e12;
for j = 1:n_j
    if y_j(j)==0
        d = n*(j-1);
        K(d+1,d+1) = K(d+1,d+1) + k_s;
        K(d+2,d+2) = K(d+2,d+2) + k_s;
        K(d+3,d+3) = K(d+3,d+3) + k_s;
    end
end

% ---------- carga vertical -Pv en el extremo de la viga ----------
F = zeros(n_g,1);
j_tip = b_n(N_B+1);
F(n*(j_tip-1)+2) = -P_v;

% ---------- solucion ----------
Z = K\F;
ux_j = zeros(n_j,1); uy_j = zeros(n_j,1); um_j = zeros(n_j,1);
for j = 1:n_j
    ux_j(j) = Z(n*(j-1)+1);
    uy_j(j) = Z(n*(j-1)+2);
    um_j(j) = sqrt(ux_j(j)^2 + uy_j(j)^2);
end
umax   = max(um_j);
flecha = abs(uy_j(j_tip));
giro   = Z(n*(corner-1)+3);
deriva = max(ux_j);
d_ref  = P_v*L_b^3/(3*E_b*I_b);
t_seg  = toc;

fprintf('==== MURO + VIGA VOLADIZO (membrana ITW drilling) ====\n');
fprintf('nudos=%d  elementos muro=%d  GDL=%d\n', n_j, n_ew, n_g);
fprintf('flecha punta viga        = %.4e m\n', flecha);
fprintf('ref viga sola (muro rig.)= %.4e m   (tip/ref = %.3f)\n', d_ref, flecha/d_ref);
fprintf('giro drilling rz esquina = %.4e rad  (FINITO -> transmite momento)\n', giro);
fprintf('deriva max del muro      = %.4e m\n', deriva);
fprintf('CHECK t_seg %.4f\n', t_seg);

% ---------- grafica: deformada coloreada por |u| ----------
amp = 0.18*sqrt(W^2 + H^2)/umax;
dx_j = x_j + amp*ux_j;
dy_j = y_j + amp*uy_j;
figure; hold on;
verts = [dx_j dy_j];
te = zeros(n_ew,1);
for e = 1:n_ew
    te(e) = mean(um_j(e_j(e,:)));
end
patch('Vertices',verts,'Faces',e_j,'FaceVertexCData',te, ...
      'FaceColor','flat','EdgeColor',[.4 .4 .4]);
for ib = 1:N_B
    p1 = b_n(ib); p2 = b_n(ib+1);
    plot([dx_j(p1) dx_j(p2)],[dy_j(p1) dy_j(p2)],'-','Color',[.8 0 0],'LineWidth',2);
end
axis equal; colormap(jet); colorbar;
xlabel('x  ancho [m]'); ylabel('z  altura [m]');
title('Muro + viga voladizo ITW - deformada (color |u|)');
hold off;
