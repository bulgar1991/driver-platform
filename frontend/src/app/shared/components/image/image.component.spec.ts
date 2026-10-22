import { TestBed } from '@angular/core/testing'
import { ImageAsset } from '@/core/models'
import { ImageComponent } from './image.component'

const photo: ImageAsset = {
  src: '/assets/images/car-960.webp',
  srcset: '/assets/images/car-480.webp 480w, /assets/images/car-960.webp 960w',
  width: 960,
  height: 540,
  placeholder: 'data:image/webp;base64,AAAA',
}

describe('ImageComponent', () => {
  const create = (image: ImageAsset | string, priority = false) => {
    const fixture = TestBed.createComponent(ImageComponent)
    fixture.componentRef.setInput('image', image)
    fixture.componentRef.setInput('alt', 'A car')
    fixture.componentRef.setInput('priority', priority)
    fixture.detectChanges()
    const host = fixture.nativeElement as HTMLElement
    return { fixture, host, img: host.querySelector('img')! }
  }

  it('shows the blurred preview until the image has loaded, then the image', () => {
    const { fixture, host, img } = create(photo)

    expect(host.style.getPropertyValue('--placeholder')).toContain(
      'data:image/webp;base64,AAAA'
    )
    expect(host.style.aspectRatio).toBe('960 / 540')
    expect(img.getAttribute('srcset')).toBe(photo.srcset)
    expect(img.getAttribute('loading')).toBe('lazy')
    expect(host.classList).not.toContain('image--loaded')

    img.dispatchEvent(new Event('load'))
    fixture.detectChanges()
    expect(host.classList).toContain('image--loaded')
  })

  it('loads a priority image first and a plain URL without a preview', () => {
    const { host, img } = create('/logos/hotel.svg', true)

    expect(img.getAttribute('src')).toBe('/logos/hotel.svg')
    expect(img.getAttribute('loading')).toBe('eager')
    expect(img.getAttribute('fetchpriority')).toBe('high')
    expect(host.style.getPropertyValue('--placeholder')).toBe('')
  })

  it('keeps the preview when the image cannot load', () => {
    const { fixture, host, img } = create(photo)
    img.dispatchEvent(new Event('error'))
    fixture.detectChanges()
    expect(host.classList).toContain('image--failed')
    expect(host.classList).not.toContain('image--loaded')
  })
})
