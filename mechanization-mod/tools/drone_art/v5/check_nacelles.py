#!/usr/bin/env python3
"""Verifies no near-nacelle pixel sits above the fuselage's top outline (the far pod's peek is allowed). Usage: check_nacelles.py"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_drone5 as d, gen_drone4 as g

A = dict(g.VARIANTS["A"], pod="capsule")
orig = d.draw_pod


def frame(near, far_on, k=0):
    d.draw_pod = lambda cv, x0, x1, y0, y1, kk, far: orig(cv, x0, x1, y0, y1, kk, far) if (far_on if far else near) else None
    return d.base_frame(A, k).im


def top(im, x):
    return next((y for y in range(im.height) if im.getpixel((x, y))[3]), 99)


bad = 0
for k in range(4):
    hull, nearonly = frame(False, False, k), frame(True, False, k)
    for x in range(hull.width):
        if top(nearonly, x) < top(hull, x):
            bad += 1; print("near pod above hull at x", x, "frame", k)
print("near pod above fuselage top:", "NONE" if not bad else bad, "(far-pod peek above the hull is intended)")
sys.exit(1 if bad else 0)
