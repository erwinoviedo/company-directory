/** A company as returned by the API. */
export interface Company {
  id: string;
  name: string;
  websiteUrl: string;
  domain: string;
  createdAt: string;
  updatedAt: string | null;
  /** Present only on free-text searches: 0 (weak match) to 1 (exact match). */
  relevanceScore?: number;
}

/** Body for creating or updating a company. */
export interface CompanyInput {
  name: string;
  websiteUrl: string;
}

/** Query-string options of GET /api/companies. */
export interface CompanySearch {
  name?: string;
  domain?: string;
  q?: string;
  page?: number;
  pageSize?: number;
}

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
