if __name__ == '__main__':
    import sys
    p = 0
    if len(sys.argv) > 1:
        p = int(sys.argv[1])

    import _sv
    _sv.main(p)
