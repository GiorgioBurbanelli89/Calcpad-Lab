import numpy as np
from scipy.integrate import quad, trapezoid, simpson, odeint
from scipy.interpolate import interp1d
from scipy.special import erf, gamma, factorial, comb
from scipy.optimize import minimize_scalar
import scipy.linalg as la
# integrate
I,e = quad(lambda x: x**2, 0, 3)
print("quad x^2 [0,3] = %.6f (ref 9)" % I)
y = np.array([0.,1,4,9,16]); x=np.array([0.,1,2,3,4])
print("trapezoid = %.4f  simpson = %.4f (ref simpson 21.33)" % (trapezoid(y,x), simpson(y,x)))
# odeint dy/dt=-y, y0=1, en t=1 -> e^-1=0.3679
sol = odeint(lambda yy,t: -yy, np.array([1.0]), np.array([0.,0.5,1.0]))
print("odeint y(1) = %.5f (ref 0.36788)" % sol[2][0])
# interpolate
f = interp1d(np.array([0.,1,2]), np.array([0.,10,40]))
print("interp1d(1.5) = %.3f (ref 25)" % f(1.5))
# special
print("erf(1)=%.5f gamma(5)=%.1f factorial(5)=%.1f comb(6,2)=%.1f" % (erf(1.0), gamma(5.0), factorial(5.0), comb(6.0,2.0)))
# optimize
print("minimize_scalar (x-3)^2 = %.4f (ref 3)" % minimize_scalar(lambda z:(z-3)**2, bounds=(-10,10)))
# linalg
A=np.array([[0.,1],[-1,0]])
print("expm([[0,1],[-1,0]])[0,0] = %.5f (ref cos1=0.5403)" % la.expm(A)[0][0])
