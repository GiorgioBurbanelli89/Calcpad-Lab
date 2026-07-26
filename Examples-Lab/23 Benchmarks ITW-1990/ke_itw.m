function K_e = ke_itw(X4, Y4, D, gam, t)
% Rigidez de elemento membrana ITW (Ibrahimbegovic-Taylor-Wilson 1990) con
% GDL drilling: 4 nudos x 3 GDL (u, v, rz) = 12x12, condensados los 2 modos burbuja.
% X4,Y4 = coords de los 4 nudos (columna); D = 3x3 constitutiva (con espesor);
% gam = E/(2(1+nu)); t = espesor.
rn = [-1 1 1 -1];   sn = [-1 -1 1 1];
g_p = [-sqrt(3/5); 0; sqrt(3/5)];
g_w = [5/9; 8/9; 5/9];
kc_arr = [2 3 4 1];  ep_arr = [4 1 2 3];
cx_e = zeros(4,1);  cy_e = zeros(4,1);
for ed = 1:4
    kc = kc_arr(ed);
    cx_e(ed) =  (Y4(kc)-Y4(ed))/8;
    cy_e(ed) = -(X4(kc)-X4(ed))/8;
end
K14 = zeros(14,14);  dJ0 = 0;
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
end
