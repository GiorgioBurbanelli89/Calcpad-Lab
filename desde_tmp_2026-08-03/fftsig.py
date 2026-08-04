import scipy, numpy as np
print("VERSION:", scipy.__version__)
# FFT
X = np.fft.fft(np.array([1.,1,1,1]))
print("fft([1,1,1,1]) abs =", [round(v,2) for v in np.abs(X)])   # [4,0,0,0]
# cos con pico
n=np.array([0.,1,2,3,4,5,6,7]); sig=np.cos(2*np.pi*2*n/8)
mag=np.abs(np.fft.fft(sig)); print("fft cos pico en bin:", int(np.argmax(mag[:5])), "(ref 2)")
print("fftfreq(4,1)=", [round(v,2) for v in np.fft.fftfreq(4,1.0)])
# signal
from scipy.signal import convolve, lfilter, butter, find_peaks
from scipy.signal.windows import hann
print("convolve =", [round(v,1) for v in convolve(np.array([1.,2,3]), np.array([0.,1,0.5]))])
b,a = butter(2, 0.5); print("butter(2,0.5) b=%s a=%s" % ([round(v,4) for v in b],[round(v,4) for v in a]))
y = lfilter(b, a, np.array([1.,0,0,0,0,0]))
print("lfilter impulso[0:3]=", [round(v,4) for v in y[:3]])
peaks,_ = find_peaks(np.array([0.,2,0,3,0,1])); print("find_peaks =", [int(v) for v in peaks], "(ref [1,3])")
print("hann(5)=", [round(v,3) for v in hann(5)])
# spatial
from scipy.spatial.distance import euclidean, cdist
print("euclidean([0,0],[3,4])=%.1f" % euclidean(np.array([0.,0]), np.array([3.,4])))
