try:
    import scipy.sparse
    print("SUITE PY: scipy.sparse OK")
except ImportError:
    print("SUITE PY: NO scipy.sparse (ImportError)")
