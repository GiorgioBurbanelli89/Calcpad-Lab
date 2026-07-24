function Fint=fint_c(u,ne,EMAT,D4,alp,kc,sig0,Bc,dJw,edof,ndof)
% Fuerza interna con return-map Drucker-Prager (formulacion de GEO5, encaje por
% extension triaxial: F = alpha*p + sqrt(J2) - k). Sin promediado nodal: el
% promediado sirve para graficar, no para el residuo (rompe la consistencia
% variacional y Newton deja de tener punto fijo).
Fint=zeros(ndof,1);
for e=1:ne
  D=D4{EMAT(e)}; mm=EMAT(e); a=alp(mm); kk=kc(mm); d=edof(e,:); ue=u(d');
  for q=1:3
    ep=Bc{e,q}*ue;
    sig=sig0(:,(e-1)*3+q)+D*[ep(1);ep(2);0;ep(3)];
    p=(sig(1)+sig(2)+sig(3))/3; s1=sig(1)-p; s2=sig(2)-p; s3=sig(3)-p;
    sq=sqrt(0.5*(s1*s1+s2*s2+s3*s3)+sig(4)*sig(4));
    F=3*a*p+sq-kk;
    if F>0
      if sq-F>1e-9; fc=(sq-F)/sq; sip=[p+s1*fc; p+s2*fc; sig(4)*fc];
      else; pa=kk/(3*a); sip=[pa;pa;0]; end
    else
      sip=[sig(1);sig(2);sig(4)];
    end
    Fint(d)=Fint(d)+Bc{e,q}'*sip*dJw(e,q);
  end
end
